using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PaletteShellExtension.Classes;

internal static partial class ScriptRunner
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public sealed class ScriptResult
    {
        public int ExitCode { get; set; }
        public string? StandardOutput { get; set; }
        public string? StandardError { get; set; }
        public bool TimedOut { get; set; }

        /// <summary>Wall-clock run time, when the runner measured it. Null for results that
        /// predate the wait (e.g. a failed start), so reports can omit the line.</summary>
        public long? DurationMs { get; set; }
    }

    /// <summary>
    /// Builds a user-facing failure message for a non-zero exit, including the script's
    /// captured stderr (trimmed) when there is any.
    /// </summary>
    public static string DescribeFailure(ScriptResult result)
    {
        var error = result.StandardError?.Trim();
        if (string.IsNullOrEmpty(error))
        {
            return $"Script failed with exit code {result.ExitCode}";
        }

        const int max = 300;
        if (error.Length > max)
        {
            error = error[..max] + "…";
        }
        return $"Script failed (exit {result.ExitCode}): {error}";
    }

    // ANSI escape sequences: CSI (colors/cursor), OSC (titles/hyperlinks), and two-character
    // escapes. PowerShell 7 colors its error output with these even when stderr is redirected,
    // and native tools a script calls (git, npm, …) do the same.
    [GeneratedRegex(@"\x1B(?:\[[0-9;?]*[ -/]*[@-~]|\][^\x07\x1B]*(?:\x07|\x1B\\)?|[@-_])")]
    private static partial Regex AnsiEscapes();

    /// <summary>
    /// Removes ANSI escape sequences from captured output. Nothing downstream can render
    /// them — toasts, dialogs, pages, and the log are all plain text — so left in they show
    /// up as literal "[31;1m" noise.
    /// </summary>
    internal static string? StripAnsi(string? text)
        => string.IsNullOrEmpty(text) || !text.Contains('\x1B') ? text : AnsiEscapes().Replace(text, "");

    /// <summary>Waits briefly for a stream read to finish, returning null on failure or
    /// timeout. Captured output is ANSI-stripped here — the single funnel for both streams.</summary>
    private static string? AwaitRead(Task<string>? task)
    {
        if (task is null)
        {
            return null;
        }

        try
        {
            return task.Wait(2000) ? StripAnsi(task.Result) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Async counterpart of <see cref="AwaitRead"/> — same brief grace period,
    /// but without pinning a thread while it waits.</summary>
    private static async Task<string?> AwaitReadAsync(Task<string>? task)
    {
        if (task is null)
        {
            return null;
        }

        try
        {
            return StripAnsi(await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Validated interpreter selection. The raw manifest/settings token
    /// (<c>"auto"</c>/<c>"pwsh"</c>/<c>"powershell"</c>) is parsed into one of these so each case
    /// gets an explicit policy instead of "anything that isn't 'powershell' means pwsh-or-fallback".
    /// </summary>
    public enum ShellHost
    {
        /// <summary>Prefer pwsh, fall back to Windows PowerShell.</summary>
        Auto,
        /// <summary>Require PowerShell 7; fail if it isn't installed.</summary>
        Pwsh,
        /// <summary>Require Windows PowerShell (powershell.exe).</summary>
        WindowsPowerShell,
        /// <summary>Unrecognized token — the script is incompatible.</summary>
        Unknown,
    }

    /// <summary>Thrown when the declared host can't be satisfied (required interpreter missing,
    /// or an unknown host token). Callers turn this into a start-failure the user can read.</summary>
    public sealed class ShellResolutionException : Exception
    {
        public ShellResolutionException(string message) : base(message) { }
    }

    /// <summary>Parses a host token into <see cref="ShellHost"/>. Null/blank is <see cref="ShellHost.Auto"/>
    /// (the manifest default). Any other unrecognized value is <see cref="ShellHost.Unknown"/> — signalled,
    /// never silently coerced to a working interpreter.</summary>
    public static ShellHost ParseHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return ShellHost.Auto;
        }

        return host.Trim().ToLowerInvariant() switch
        {
            "auto" => ShellHost.Auto,
            "pwsh" => ShellHost.Pwsh,
            "powershell" => ShellHost.WindowsPowerShell,
            _ => ShellHost.Unknown,
        };
    }

    /// <summary>
    /// Resolves the host token to a concrete interpreter path.
    /// <list type="bullet">
    /// <item>Auto: pwsh, else Windows PowerShell.</item>
    /// <item>Pwsh: pwsh only — throws "PowerShell 7 is required but not installed" when missing,
    /// rather than silently downgrading a script that declared a 7-only requirement.</item>
    /// <item>WindowsPowerShell: powershell.exe only.</item>
    /// <item>Unknown: throws — the script is incompatible.</item>
    /// </list>
    /// Each call re-resolves against the current PATH and standard install locations, so an
    /// interpreter installed after startup is picked up (no permanently cached result).
    /// </summary>
    public static string ResolveShell(string? host)
    {
        switch (ParseHost(host))
        {
            case ShellHost.WindowsPowerShell:
                return FindWindowsPowerShell()
                    ?? throw new ShellResolutionException("Windows PowerShell (powershell.exe) is required but was not found.");

            case ShellHost.Pwsh:
                return FindPwsh()
                    ?? throw new ShellResolutionException("PowerShell 7 is required but not installed. Install it from https://aka.ms/powershell, or set the script host to 'auto'.");

            case ShellHost.Auto:
                return FindPwsh() ?? FindWindowsPowerShell()
                    ?? throw new ShellResolutionException("No PowerShell interpreter (pwsh.exe or powershell.exe) was found.");

            default:
                return throwUnknown();
        }

        string throwUnknown() =>
            throw new ShellResolutionException($"Unknown script host '{host}'. Supported values are 'auto', 'pwsh', and 'powershell'.");
    }

    /// <summary>Best-effort interpreter name for display (the failure report) that never throws:
    /// returns the resolved path when available, otherwise the interpreter that <em>would</em> be
    /// used, or a marker for an unknown host. Kept separate from <see cref="ResolveShell"/> so
    /// report generation can't fail on a missing/invalid interpreter.</summary>
    public static string DescribeShell(string? host)
    {
        try
        {
            return ResolveShell(host);
        }
        catch (ShellResolutionException)
        {
            return ParseHost(host) switch
            {
                ShellHost.Pwsh => "pwsh.exe (not found)",
                ShellHost.WindowsPowerShell => "powershell.exe (not found)",
                ShellHost.Auto => "(no PowerShell found)",
                _ => $"(unknown host '{host}')",
            };
        }
    }

    /// <summary>Finds pwsh.exe on PATH or in the standard PowerShell 7+ install locations.</summary>
    private static string? FindPwsh() => FindExecutable("pwsh.exe", PwshInstallDirs());

    /// <summary>Finds powershell.exe on PATH or in its fixed System32 location.</summary>
    private static string? FindWindowsPowerShell() => FindExecutable("powershell.exe", WindowsPowerShellInstallDirs());

    /// <summary>Standard install roots for PowerShell 7+: <c>%ProgramFiles%\PowerShell\&lt;version&gt;</c>
    /// (both bitnesses) and the winget/WindowsApps shim location.</summary>
    private static IEnumerable<string> PwshInstallDirs()
    {
        foreach (var pf in new[]
                 {
                     Environment.GetEnvironmentVariable("ProgramW6432"),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                 })
        {
            if (string.IsNullOrWhiteSpace(pf))
            {
                continue;
            }

            var root = Path.Combine(pf, "PowerShell");
            string[] versionDirs;
            try
            {
                versionDirs = Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>();
            }
            catch (Exception)
            {
                versionDirs = Array.Empty<string>();
            }

            foreach (var dir in versionDirs)
            {
                yield return dir;
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "Microsoft", "WindowsApps");
        }
    }

    /// <summary>Windows PowerShell ships at a single fixed path under the OS directory.</summary>
    private static IEnumerable<string> WindowsPowerShellInstallDirs()
    {
        var windir = Environment.GetEnvironmentVariable("SystemRoot")
                     ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windir))
        {
            yield return Path.Combine(windir, "System32", "WindowsPowerShell", "v1.0");
        }
    }

    /// <summary>Resolves <paramref name="exe"/> to a full path by scanning the current PATH first,
    /// then <paramref name="extraDirs"/> (standard install locations). Returns null when not found.
    /// Resolved fresh on every call — nothing is cached, so a newly installed interpreter is seen.</summary>
    private static string? FindExecutable(string exe, IEnumerable<string> extraDirs)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (Contains(dir, exe, out var hit))
                {
                    return hit;
                }
            }
        }

        foreach (var dir in extraDirs)
        {
            if (Contains(dir, exe, out var hit))
            {
                return hit;
            }
        }

        return null;

        static bool Contains(string? dir, string exe, out string? fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(dir))
            {
                return false;
            }

            try
            {
                var candidate = Path.Combine(dir, exe);
                if (File.Exists(candidate))
                {
                    fullPath = candidate;
                    return true;
                }
            }
            catch (Exception)
            {
                // Ignore malformed PATH entries / install paths.
            }

            return false;
        }
    }

    /// <summary>
    /// Builds a PowerShell preflight that fails the run (exit 1) with an Install-Module hint
    /// for the first required module that isn't available, or an empty string when nothing is
    /// required. Names are embedded as single-quoted literals with quotes doubled, so an odd
    /// module name can't break out of the string or inject commands.
    /// </summary>
    private static string BuildModuleCheck(IReadOnlyList<string>? requiredModules)
    {
        if (requiredModules is null || requiredModules.Count == 0)
        {
            return "";
        }

        var quoted = new List<string>(requiredModules.Count);
        foreach (var name in requiredModules)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            quoted.Add(PowerShellQuoting.SingleQuote(name));
        }

        if (quoted.Count == 0)
        {
            return "";
        }

        var list = string.Join(",", quoted);
        return "foreach ($__psRequired in @(" + list + ")) { " +
               "if (-not (Get-Module -ListAvailable -Name $__psRequired)) { " +
               "Write-Error \"Missing required module '$__psRequired'. Install it with:  Install-Module -Name $__psRequired -Scope CurrentUser\"; " +
               "exit 1 } }; ";
    }

    public static ProcessStartInfo BuildProcessStartInfo(
        string scriptPath,
        string args,
        string host,
        string? cwd,
        Dictionary<string, string>? env = null,
        bool requiresAdmin = false,
        bool captureOutput = false,
        IReadOnlyList<string>? requiredModules = null)
    {
        var shell = ResolveShell(host);
        var psi = new ProcessStartInfo(shell);

        // Always use STA mode - scripts may call clipboard functions internally
        psi.ArgumentList.Add("-STA");
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");

        // Pre-load the module so attributes can be resolved at parse time
        var scriptDir = Path.GetDirectoryName(scriptPath) ?? "";
        var modulePath = Path.Combine(scriptDir, "PaletteScriptAttributes.psm1");
        var usingModule = File.Exists(modulePath) ? $"using module {PowerShellQuoting.SingleQuote(modulePath)}; " : "";

        // Declared [RequiresModule(...)] dependencies are checked before the script runs, so a
        // missing module fails with an actionable Install-Module hint instead of the script's
        // own cryptic "term not recognized" error. Uses PowerShell's own module resolution.
        var moduleCheck = BuildModuleCheck(requiredModules);

        psi.ArgumentList.Add("-Command");
        // Import the module when present (the `using` statement must come first), run any
        // required-module preflight, force UTF-8 console output so captured stdout isn't
        // mangled, then dot-source the script with args. Always redirect the information
        // stream (6) to stdout to capture Write-Host. `args` is already single-quoted per
        // value by the caller, so it's interpolated into this single command string rather
        // than re-split into ArgumentList entries (which would break values with spaces).
        var commandString = $"{usingModule}{moduleCheck}[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; . {PowerShellQuoting.SingleQuote(scriptPath)} {args} 6>&1";
        psi.ArgumentList.Add(commandString);

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            psi.WorkingDirectory = cwd;
        }

        if (env is not null && env.Count > 0)
        {
            foreach (var kv in env)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
        }

        // Elevation vs. output capture
        if (requiresAdmin)
        {
            psi.UseShellExecute = true;
            psi.Verb = "runas";
        }
        else
        {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            if (captureOutput)
            {
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                // Decode as UTF-8 to match the console encoding forced above.
                psi.StandardOutputEncoding = Utf8NoBom;
                psi.StandardErrorEncoding = Utf8NoBom;
            }
        }

        return psi;
    }

    public static bool RunScript(
        string scriptPath,
        string args,
        string host,
        string? cwd,
        Dictionary<string, string>? env = null,
        bool requiresAdmin = false,
        IReadOnlyList<string>? requiredModules = null)
    {
        try
        {
            var psi = BuildProcessStartInfo(scriptPath, args, host, cwd, env, requiresAdmin: requiresAdmin, requiredModules: requiredModules);
            // Fire-and-forget: dispose the handle (this does not stop the child) so we
            // don't leak the Process object the caller never uses.
            using var proc = Process.Start(psi);
            return proc is not null;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to launch script '{scriptPath}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Async counterpart of <see cref="RunScriptAndWait"/> for callers that already run off
    /// the UI path (the List/Markdown/Result pages): the wait for the child process is
    /// awaited rather than blocked on, so a slow script doesn't pin a threadpool thread for
    /// its whole run (up to the timeout) per in-flight query. The synchronous version stays
    /// for the toolkit's <c>Invoke()</c> entry points, which are inherently blocking.
    /// </summary>
    public static async Task<ScriptResult?> RunScriptAndWaitAsync(
        string scriptPath,
        string args,
        string host,
        string? cwd,
        Dictionary<string, string>? env = null,
        bool requiresAdmin = false,
        int? timeoutMs = null,
        bool reportProgress = true,
        IReadOnlyList<string>? requiredModules = null,
        CancellationToken cancellationToken = default)
    {
        Process? proc = null;

        // Same hard ceiling as the synchronous runner: a null or oversized timeout is
        // clamped to MaxTimeoutMs so no async path can wait forever.
        var effectiveTimeoutMs = Math.Min(
            timeoutMs ?? PowerShellScriptParser.MaxTimeoutMs,
            PowerShellScriptParser.MaxTimeoutMs);

        var stopwatch = Stopwatch.StartNew();

        var progress = reportProgress
            ? ScriptStatus.ShowRunning(Path.GetFileNameWithoutExtension(scriptPath))
            : null;

        try
        {
            var psi = BuildProcessStartInfo(
                scriptPath: scriptPath,
                args: args,
                host: host,
                cwd: cwd,
                env: env,
                requiresAdmin: requiresAdmin,
                captureOutput: !requiresAdmin,
                requiredModules: requiredModules);

            proc = Process.Start(psi);

            if (proc == null)
            {
                return null;
            }

            var result = new ScriptResult();

            // Same deadlock/timeout reasoning as the synchronous version: kick off both
            // stream reads before waiting so a full pipe buffer can't wedge the child and
            // a hung child can't defeat the timeout.
            Task<string>? stdoutTask = null;
            Task<string>? stderrTask = null;
            if (!requiresAdmin)
            {
                stdoutTask = proc.StandardOutput.ReadToEndAsync();
                stderrTask = proc.StandardError.ReadToEndAsync();
            }

            try
            {
                await proc.WaitForExitAsync()
                    .WaitAsync(TimeSpan.FromMilliseconds(effectiveTimeoutMs), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                result.TimedOut = true;
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                try { proc.Kill(entireProcessTree: true); }
                catch (Exception)
                {
                    // Ignore failures killing the process tree.
                }
                // Killing closes the pipes, so the reads complete with whatever was
                // buffered; capture that partial output before returning. The Warn comes
                // after the drain so it can include the script's stderr.
                result.StandardOutput = await AwaitReadAsync(stdoutTask).ConfigureAwait(false);
                result.StandardError = await AwaitReadAsync(stderrTask).ConfigureAwait(false);
                Log.Warn($"Script '{scriptPath}' timed out after {stopwatch.ElapsedMilliseconds}ms (limit {effectiveTimeoutMs}ms) and was killed{ScriptFailureReport.StderrForLog(result.StandardError)}");
                return result;
            }
            catch (OperationCanceledException)
            {
                // Caller cancelled (e.g. a live-provider page superseded this run). Kill the
                // orphaned child so it isn't left running, then propagate the cancellation.
                try { proc.Kill(entireProcessTree: true); }
                catch (Exception)
                {
                    // Ignore failures killing the process tree.
                }
                throw;
            }

            // The process has exited, so the streams are closed and the reads finish
            // promptly; this also ensures async I/O completion before we read ExitCode.
            result.StandardOutput = await AwaitReadAsync(stdoutTask).ConfigureAwait(false);
            result.StandardError = await AwaitReadAsync(stderrTask).ConfigureAwait(false);
            result.ExitCode = proc.ExitCode;
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            if (result.ExitCode != 0)
            {
                Log.Warn($"Script '{scriptPath}' exited with code {result.ExitCode}{ScriptFailureReport.StderrForLog(result.StandardError)}");
            }
            return result;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to run script '{scriptPath}'", ex);
            return null;
        }
        finally
        {
            proc?.Dispose();
            ScriptStatus.Hide(progress);
        }
    }

    public static ScriptResult? RunScriptAndWait(
        string scriptPath,
        string args,
        string host,
        string? cwd,
        Dictionary<string, string>? env = null,
        bool requiresAdmin = false,
        int? timeoutMs = null,
        bool reportProgress = true,
        IReadOnlyList<string>? requiredModules = null)
    {
        Process? proc = null;

        var scriptName = Path.GetFileNameWithoutExtension(scriptPath);

        // This wait happens on a thread the host is synchronously blocked on (a COM call),
        // so it must always be bounded: a null or oversized timeout gets the same hard
        // ceiling a script-declared [ScriptTimeout(...)] does. No code path waits forever.
        var effectiveTimeoutMs = Math.Min(
            timeoutMs ?? PowerShellScriptParser.MaxTimeoutMs,
            PowerShellScriptParser.MaxTimeoutMs);

        // One line at start and one at finish, so a hang or crash report can be lined up
        // against the log: a start line with no matching finish points at the culprit.
        Log.Info($"Running '{scriptName}' (host {host}, timeout {effectiveTimeoutMs}ms)");
        var stopwatch = Stopwatch.StartNew();

        // Show a "Running <script>…" spinner in the status bar for the duration of the
        // wait. Folded in here (rather than per-script) so every output mode benefits —
        // a slow script no longer looks frozen until its toast/page appears.
        var progress = reportProgress
            ? ScriptStatus.ShowRunning(scriptName)
            : null;

        try
        {
            var psi = BuildProcessStartInfo(
                scriptPath: scriptPath,
                args: args,
                host: host,
                cwd: cwd,
                env: env,
                requiresAdmin: requiresAdmin,
                captureOutput: !requiresAdmin,
                requiredModules: requiredModules);

            proc = Process.Start(psi);

            if (proc == null)
            {
                Log.Warn($"Script '{scriptName}' failed to start after {stopwatch.ElapsedMilliseconds}ms (Process.Start returned null)");
                return null;
            }

            var result = new ScriptResult();

            // Read both streams asynchronously. Reading synchronously to EOF before
            // waiting would (a) deadlock if the child fills one pipe buffer while we
            // block on the other, and (b) defeat the timeout entirely for a hung child,
            // since ReadToEnd() blocks until the stream closes. Kicking off the reads
            // here lets the WaitForExit timeout below do its job.
            Task<string>? stdoutTask = null;
            Task<string>? stderrTask = null;
            if (!requiresAdmin)
            {
                stdoutTask = proc.StandardOutput.ReadToEndAsync();
                stderrTask = proc.StandardError.ReadToEndAsync();
            }

            if (!proc.WaitForExit(effectiveTimeoutMs))
            {
                result.TimedOut = true;
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                try { proc.Kill(entireProcessTree: true); }
                catch (Exception)
                {
                    // Ignore failures killing the process tree.
                }
                // Killing closes the pipes, so the reads complete with whatever was
                // buffered; capture that partial output before returning. The Warn comes
                // after the drain so it can include the script's stderr.
                result.StandardOutput = AwaitRead(stdoutTask);
                result.StandardError = AwaitRead(stderrTask);
                Log.Warn($"Script '{scriptName}' timed out after {stopwatch.ElapsedMilliseconds}ms (limit {effectiveTimeoutMs}ms) and was killed{ScriptFailureReport.StderrForLog(result.StandardError)}");
                return result;
            }

            // The process has exited, so the streams are closed and the reads finish
            // promptly; this also ensures async I/O completion before we read ExitCode.
            result.StandardOutput = AwaitRead(stdoutTask);
            result.StandardError = AwaitRead(stderrTask);
            result.ExitCode = proc.ExitCode;
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            if (result.ExitCode != 0)
            {
                Log.Warn($"Script '{scriptName}' exited with code {result.ExitCode} after {stopwatch.ElapsedMilliseconds}ms{ScriptFailureReport.StderrForLog(result.StandardError)}");
            }
            else
            {
                Log.Info($"Script '{scriptName}' completed in {stopwatch.ElapsedMilliseconds}ms");
            }
            return result;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to run script '{scriptPath}' after {stopwatch.ElapsedMilliseconds}ms", ex);
            return null;
        }
        finally
        {
            proc?.Dispose();
            ScriptStatus.Hide(progress);
        }
    }
}

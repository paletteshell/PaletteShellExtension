using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PaletteShellExtension.Classes;

internal static class ScriptRunner
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public sealed class ScriptResult
    {
        public int ExitCode { get; set; }
        public string? StandardOutput { get; set; }
        public string? StandardError { get; set; }
        public bool TimedOut { get; set; }
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

    /// <summary>Waits briefly for a stream read to finish, returning null on failure or timeout.</summary>
    private static string? AwaitRead(Task<string>? task)
    {
        if (task is null)
        {
            return null;
        }

        try
        {
            return task.Wait(2000) ? task.Result : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static readonly Lazy<bool> PwshAvailable = new(() => CanResolveOnPath("pwsh.exe"));

    public static string ResolveShell(string? host)
    {
        if (string.Equals(host, "powershell", StringComparison.OrdinalIgnoreCase))
        {
            return "powershell.exe";
        }

        // Default to PowerShell 7 (pwsh), but fall back to Windows PowerShell when it
        // isn't installed so scripts still run on a stock machine.
        return PwshAvailable.Value ? "pwsh.exe" : "powershell.exe";
    }

    private static bool CanResolveOnPath(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (var dir in path.Split(Path.PathSeparator))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir, exe)))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Ignore malformed PATH entries.
            }
        }

        return false;
    }

    public static ProcessStartInfo BuildProcessStartInfo(
        string scriptPath,
        string args,
        string host,
        string? cwd,
        Dictionary<string, string>? env = null,
        bool requiresAdmin = false,
        bool captureOutput = false)
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
        var usingModule = File.Exists(modulePath) ? $"using module '{modulePath}'; " : "";

        psi.ArgumentList.Add("-Command");
        // Import the module when present (the `using` statement must come first), force
        // UTF-8 console output so captured stdout isn't mangled, then dot-source the script
        // with args. Always redirect the information stream (6) to stdout to capture
        // Write-Host. `args` is already single-quoted per value by the caller, so it's
        // interpolated into this single command string rather than re-split into
        // ArgumentList entries (which would break values containing spaces).
        var commandString = $"{usingModule}[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; . '{scriptPath}' {args} 6>&1";
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
        Dictionary<string, string>? env = null)
    {
        try
        {
            var psi = BuildProcessStartInfo(scriptPath, args, host, cwd, env);
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

    public static ScriptResult? RunScriptAndWait(
        string scriptPath,
        string args,
        string host,
        string? cwd,
        Dictionary<string, string>? env = null,
        bool requiresAdmin = false,
        int? timeoutMs = null,
        bool reportProgress = true)
    {
        Process? proc = null;

        // Show a "Running <script>…" spinner in the status bar for the duration of the
        // wait. Folded in here (rather than per-script) so every output mode benefits —
        // a slow script no longer looks frozen until its toast/page appears.
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
                captureOutput: !requiresAdmin);

            proc = Process.Start(psi);

            if (proc == null)
            {
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

            // Wait for exit, enforcing the timeout if one was given.
            if (timeoutMs.HasValue && !proc.WaitForExit(timeoutMs.Value))
            {
                result.TimedOut = true;
                Log.Warn($"Script '{scriptPath}' timed out after {timeoutMs.Value}ms and was killed");
                try { proc.Kill(entireProcessTree: true); }
                catch (Exception)
                {
                    // Ignore failures killing the process tree.
                }
                // Killing closes the pipes, so the reads complete with whatever was
                // buffered; capture that partial output before returning.
                result.StandardOutput = AwaitRead(stdoutTask);
                result.StandardError = AwaitRead(stderrTask);
                return result;
            }

            if (!timeoutMs.HasValue)
            {
                proc.WaitForExit();
            }

            // The process has exited, so the streams are closed and the reads finish
            // promptly; this also ensures async I/O completion before we read ExitCode.
            result.StandardOutput = AwaitRead(stdoutTask);
            result.StandardError = AwaitRead(stderrTask);
            result.ExitCode = proc.ExitCode;
            if (result.ExitCode != 0)
            {
                Log.Warn($"Script '{scriptPath}' exited with code {result.ExitCode}");
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
}

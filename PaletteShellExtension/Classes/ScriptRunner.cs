using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PaletteShellExtension.Classes;

internal static class ScriptRunner
{
    public class ScriptResult
    {
        public int ExitCode { get; set; }
        public string? StandardOutput { get; set; }
        public string? StandardError { get; set; }
        public bool TimedOut { get; set; }
    }

    public static string ResolveShell(string? host)
    {
        return string.Equals(host, "powershell", StringComparison.OrdinalIgnoreCase)
            ? "powershell.exe"
            : "pwsh.exe";
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
        if (File.Exists(modulePath))
        {
            psi.ArgumentList.Add("-Command");
            // Import module, then dot-source the script with args
            // Always redirect information stream (6) to stdout to capture Write-Host
            var commandString = $"using module '{modulePath}'; . '{scriptPath}' {args} 6>&1";
            psi.ArgumentList.Add(commandString);
        }
        else
        {
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(scriptPath);

            // Add arguments
            if (!string.IsNullOrWhiteSpace(args))
            {
                foreach (var arg in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    psi.ArgumentList.Add(arg);
                }
            }
        }

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
            }
        }

        return psi;
    }

    public static Process? RunScript(
        string scriptPath,
        string args,
        string host,
        string? cwd,
        Dictionary<string, string>? env = null)
    {
        try
        {
            var psi = BuildProcessStartInfo(scriptPath, args, host, cwd, env);
            return Process.Start(psi);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static ScriptResult? RunScriptAndWait(
        string scriptPath,
        string args,
        string host,
        string? cwd,
        Dictionary<string, string>? env = null,
        bool requiresAdmin = false,
        int? timeoutMs = null)
    {
        Process? proc = null;

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

            // Capture output if not admin
            if (!requiresAdmin)
            {
                try
                {
                    result.StandardOutput = proc.StandardOutput?.ReadToEnd();
                }
                catch (Exception)
                {
                    // Ignore failures reading stdout.
                }

                try
                {
                    result.StandardError = proc.StandardError?.ReadToEnd();
                }
                catch (Exception)
                {
                    // Ignore failures reading stderr.
                }
            }

            // Wait for exit
            if (timeoutMs.HasValue)
            {
                if (!proc.WaitForExit(timeoutMs.Value))
                {
                    result.TimedOut = true;
                    try { proc.Kill(entireProcessTree: true); }
                    catch (Exception)
                    {
                        // Ignore failures killing the process tree.
                    }
                    return result;
                }
                // Call WaitForExit() again to ensure async I/O completion
                proc.WaitForExit();
            }
            else
            {
                proc.WaitForExit();
            }

            result.ExitCode = proc.ExitCode;
            return result;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            proc?.Dispose();
        }
    }
}

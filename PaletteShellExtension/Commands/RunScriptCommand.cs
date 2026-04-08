using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using PaletteShellExtension.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PaletteShellExtension.Commands;


internal sealed partial class RunScriptCommand(string path, ScriptManifest? manifest) : InvokableCommand
{
    private static readonly Dictionary<string, SemaphoreSlim> _mutexes = new();
    private readonly ScriptManifest _manifest = manifest ?? new ScriptManifest();

    public override string Name => $"Run {Path.GetFileNameWithoutExtension(path)}";
    public override IconInfo Icon => new(_manifest.IconGlyph ?? "\uE7C3"); // Play; or map emoji Icon if you like

    public override CommandResult Invoke()
    {
        Debug.WriteLine("[RunScript] ===== RunScriptCommand.Invoke START =====");
        Debug.WriteLine($"[RunScript] Script path: {path}");
        Debug.WriteLine($"[RunScript] Metadata: Host={_manifest.Host}, Output={_manifest.Output}, Mutex={_manifest.Mutex}");

        try
        {
            var wantsClipboard = string.Equals(_manifest.Output, "Clipboard", StringComparison.OrdinalIgnoreCase);
            var wantsAdmin = _manifest.RequiresAdmin == true;
            Debug.WriteLine($"[RunScript] WantsAdmin={wantsAdmin}");

            // CWD
            var cwd = ExpandPathTokens(_manifest.Cwd, path);
            if (!string.IsNullOrWhiteSpace(cwd))
            {
                Debug.WriteLine($"[RunScript] Setting working directory: {cwd}");
            }

            // Env
            var expandedEnv = new Dictionary<string, string>();
            foreach (var kv in _manifest.Env)
            {
                var expandedValue = ExpandPathTokens(kv.Value, path) ?? "";
                Debug.WriteLine($"[RunScript] Setting env: {kv.Key}={expandedValue}");
                expandedEnv[kv.Key] = expandedValue;
            }

            var timeout = _manifest.TimeoutMs is > 0 ? _manifest.TimeoutMs!.Value : (int?)null;

            // If no timeout, run fire-and-forget
            if (timeout is null)
            {
                Debug.WriteLine("[RunScript] Fire-and-forget mode, starting process...");
                ScriptRunner.RunScript(
                    scriptPath: path,
                    args: "",
                    host: _manifest.Host ?? "pwsh",
                    cwd: cwd,
                    env: expandedEnv);
                Debug.WriteLine("[RunScript] ===== RunScriptCommand.Invoke END (Fire-and-forget) =====");
                return CommandResult.ShowToast("Script Completed");
            }

            // Run with timeout and capture output
            Debug.WriteLine($"[RunScript] Running script with timeout: {timeout.Value}ms");
            var result = ScriptRunner.RunScriptAndWait(
                scriptPath: path,
                args: "",
                host: _manifest.Host ?? "pwsh",
                cwd: cwd,
                env: expandedEnv,
                requiresAdmin: wantsAdmin,
                timeoutMs: timeout.Value);

            if (result == null)
            {
                Debug.WriteLine("[RunScript] ERROR: RunScriptAndWait returned null");
                return CommandResult.ShowToast("Error: Process.Start returned null");
            }

            if (result.TimedOut)
            {
                Debug.WriteLine($"[RunScript] Script timed out after {timeout.Value}ms");
                Debug.WriteLine("[RunScript] ===== RunScriptCommand.Invoke END (Timeout) =====");
                return CommandResult.ShowToast("Script timed out");
            }

            Debug.WriteLine($"[RunScript] Process exited with code: {result.ExitCode}");

            // If script failed, log error and return gracefully without further processing
            if (result.ExitCode != 0)
            {
                Debug.WriteLine($"[RunScript] ===== SCRIPT FAILED WITH EXIT CODE {result.ExitCode} =====");
                if (!string.IsNullOrEmpty(result.StandardError))
                {
                    Debug.WriteLine("[RunScript] Script error output:");
                    Debug.WriteLine(result.StandardError);
                }
                Debug.WriteLine("[RunScript] Returning without processing output due to script failure");
                Debug.WriteLine("[RunScript] ===== RunScriptCommand.Invoke END (Script Failed) =====");
                return CommandResult.ShowToast($"Script failed with exit code {result.ExitCode}");
            }

            // Always show toast with the captured output (includes Write-Host)
            var commandResult = !wantsAdmin && !string.IsNullOrEmpty(result.StandardOutput)
                ? CommandResult.ShowToast(result.StandardOutput)
                : CommandResult.ShowToast("Script completed");
            Debug.WriteLine("[RunScript] CommandResult created successfully");
            Debug.WriteLine("[RunScript] ===== RunScriptCommand.Invoke END (Success) =====");
            return commandResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RunScript] ===== EXCEPTION in Invoke() =====");
            Debug.WriteLine($"[RunScript] Exception type: {ex.GetType().FullName}");
            Debug.WriteLine($"[RunScript] Exception message: {ex.Message}");
            Debug.WriteLine($"[RunScript] Stack trace:");
            Debug.WriteLine(ex.StackTrace);
            Debug.WriteLine("[RunScript] ===== EXCEPTION END =====");
            throw;
        }
    }

    private static string? ExpandPathTokens(string? path, string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        var scriptDir = Path.GetDirectoryName(scriptPath) ?? "";
        return path.Replace("{ScriptDir}", scriptDir)
                   .Replace("{Home}", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                   .Replace("{Temp}", Path.GetTempPath());
    }

    private static void TrySetClipboard(string text)
    {
        Debug.WriteLine($"[RunScript] TrySetClipboard called with {text?.Length ?? 0} chars");
        try
        {
            Debug.WriteLine($"[RunScript] Attempting to set clipboard via TextCopy...");
            TextCopy.ClipboardService.SetText(text ?? "");
            Debug.WriteLine($"[RunScript] Clipboard set successfully via TextCopy");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RunScript] Clipboard operation failed: {ex.Message}");
            Debug.WriteLine($"[RunScript] Stack trace: {ex.StackTrace}");
        }
    }
}

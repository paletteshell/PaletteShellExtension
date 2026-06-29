using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using PaletteShellExtension.Pages;
using System;
using System.Collections.Generic;
using System.IO;

namespace PaletteShellExtension.Commands;


internal sealed partial class RunScriptCommand(string path, ScriptManifest? manifest) : InvokableCommand
{
    private readonly ScriptManifest _manifest = manifest ?? new ScriptManifest();

    public override string Name => $"Run {Path.GetFileNameWithoutExtension(path)}";
    public override IconInfo Icon => new(_manifest.IconGlyph ?? ""); // Play; or map emoji Icon if you like

    public override CommandResult Invoke()
    {
        var wantsClipboard = string.Equals(_manifest.Output, "Clipboard", StringComparison.OrdinalIgnoreCase);
        var wantsAdmin = _manifest.RequiresAdmin == true;

        // CWD
        var cwd = ExpandPathTokens(_manifest.Cwd, path);

        // Env
        var expandedEnv = new Dictionary<string, string>();
        foreach (var kv in _manifest.Env)
        {
            expandedEnv[kv.Key] = ExpandPathTokens(kv.Value, path) ?? "";
        }

        var timeout = _manifest.TimeoutMs is > 0 ? _manifest.TimeoutMs!.Value : (int?)null;

        // If no timeout, run fire-and-forget
        if (timeout is null)
        {
            ScriptRunner.RunScript(
                scriptPath: path,
                args: "",
                host: _manifest.Host ?? "pwsh",
                cwd: cwd,
                env: expandedEnv);
            return CommandResult.ShowToast("Script Completed");
        }

        // Run with timeout and capture output
        var result = ScriptRunner.RunScriptAndWait(
            scriptPath: path,
            args: "",
            host: _manifest.Host ?? "pwsh",
            cwd: cwd,
            env: expandedEnv,
            requiresAdmin: wantsAdmin,
            timeoutMs: timeout.Value);

        if (result == null)
            return CommandResult.ShowToast("Error: Process.Start returned null");

        if (result.TimedOut)
            return CommandResult.ShowToast("Script timed out");

        // If script failed, return gracefully without further processing
        if (result.ExitCode != 0)
            return CommandResult.ShowToast($"Script failed with exit code {result.ExitCode}");

        var output = !wantsAdmin ? result.StandardOutput : null;

        if (wantsClipboard && !string.IsNullOrEmpty(output))
            TrySetClipboard(output);

        return !string.IsNullOrEmpty(output)
            ? CommandResult.ShowToast(wantsClipboard ? "Copied to clipboard" : output)
            : CommandResult.ShowToast("Script completed");
    }

    private static string? ExpandPathTokens(string? path, string scriptPath)
        => PowerShellScriptParser.ExpandPathTokens(path, scriptPath);

    private static void TrySetClipboard(string text)
    {
        try
        {
            TextCopy.ClipboardService.SetText(text ?? "");
        }
        catch (Exception)
        {
            // Clipboard access can fail; ignore and continue.
        }
    }
}

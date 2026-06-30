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

    // Used when a script must be waited on to honor its output mode but declared no timeout.
    private const int DefaultTimeoutMs = 30000;

    public override string Name => $"Run {Path.GetFileNameWithoutExtension(path)}";
    public override IconInfo Icon => new(_manifest.IconGlyph ?? ""); // Play; or map emoji Icon if you like

    public override CommandResult Invoke()
    {
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

        // "None" never surfaces output, so when there's also no timeout we can run
        // fire-and-forget without waiting for/capturing stdout. Any other mode
        // (Toast/Clipboard) needs the output, so we must wait even without a timeout.
        var surfacesOutput = !string.Equals(_manifest.Output, "None", StringComparison.OrdinalIgnoreCase);
        if (timeout is null && !surfacesOutput)
        {
            ScriptRunner.RunScript(
                scriptPath: path,
                args: "",
                host: _manifest.Host ?? "pwsh",
                cwd: cwd,
                env: expandedEnv);
            return CommandResult.ShowToast("Script completed");
        }

        // Wait so the declared output mode can be honored, falling back to a default
        // timeout when the script didn't specify one.
        var result = ScriptRunner.RunScriptAndWait(
            scriptPath: path,
            args: "",
            host: _manifest.Host ?? "pwsh",
            cwd: cwd,
            env: expandedEnv,
            requiresAdmin: wantsAdmin,
            timeoutMs: timeout ?? DefaultTimeoutMs);

        if (result == null)
            return CommandResult.ShowToast("Error: Process.Start returned null");

        if (result.TimedOut)
            return CommandResult.ShowToast("Script timed out");

        // If script failed, return gracefully without further processing
        if (result.ExitCode != 0)
            return CommandResult.ShowToast(ScriptRunner.DescribeFailure(result));

        // Elevated scripts can't have their output captured, so suppress output handling.
        var output = !wantsAdmin ? result.StandardOutput : null;

        return ScriptOutputHandler.ToResult(_manifest.Output, output);
    }

    private static string? ExpandPathTokens(string? path, string scriptPath)
        => PowerShellScriptParser.ExpandPathTokens(path, scriptPath);
}

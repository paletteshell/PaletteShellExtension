using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System.IO;

namespace PaletteShellExtension.Commands;


/// <summary>
/// Runs a no-parameter script fire-and-forget: it has nothing to wait for or surface (output
/// mode <c>None</c> with no declared timeout), so it launches the script and reports completion
/// without ever blocking the host's COM call. Every waited ambient mode (Toast/Clipboard/Open/File,
/// or None with a declared timeout) is routed to <see cref="AmbientRunCommand"/> instead, which
/// dismisses the palette and runs asynchronously so a slow script can't freeze it.
/// </summary>
internal sealed partial class RunScriptCommand(string path, ScriptManifest? manifest) : InvokableCommand
{
    private readonly ScriptManifest _manifest = manifest ?? new ScriptManifest();

    public override string Name => $"Run {Path.GetFileNameWithoutExtension(path)}";
    public override IconInfo Icon => new(_manifest.IconGlyph ?? ""); // Play; or map emoji Icon if you like

    public override CommandResult Invoke()
    {
        // Destructive scripts gate behind a confirmation dialog; only the dialog's
        // primary command actually runs the script.
        if (!string.IsNullOrWhiteSpace(_manifest.ConfirmMessage))
        {
            var scriptName = Path.GetFileNameWithoutExtension(path);
            return CommandResult.Confirm(new ConfirmationArgs
            {
                Title = $"Run {scriptName}?",
                Description = _manifest.ConfirmMessage,
                PrimaryCommand = new CallbackCommand($"Run {scriptName}", RunNow),
                IsPrimaryCommandCritical = true,
            });
        }

        return RunNow();
    }

    private CommandResult RunNow()
    {
        var plan = ScriptExecutionService.CreatePlan(_manifest, path);

        // Launch and forget: there's no output to capture and no declared timeout to honor, so a
        // synchronous wait would only block the host for nothing.
        var started = ScriptExecutionService.RunFireAndForget(plan);
        return started
            ? AmbientRunner.Toast("Script completed")
            : ScriptFailurePresenter.ToCommandResult(path, plan.Host, "", (ScriptRunner.ScriptResult?)null);
    }
}

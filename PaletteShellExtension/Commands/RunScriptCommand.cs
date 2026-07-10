using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using PaletteShellExtension.Pages;
using System.IO;

namespace PaletteShellExtension.Commands;


internal sealed partial class RunScriptCommand(string path, ScriptManifest? manifest) : InvokableCommand
{
    private readonly ScriptManifest _manifest = manifest ?? new ScriptManifest();

    public override string Name => $"Run {Path.GetFileNameWithoutExtension(path)}";
    public override IconInfo Icon => new(_manifest.IconGlyph ?? ""); // Play; or map emoji Icon if you like

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

        // "None" never surfaces output, so when there's also no declared timeout we can run
        // fire-and-forget without waiting for/capturing stdout. Any other mode
        // (Toast/Clipboard) needs the output, so we must wait even without a timeout.
        if (plan.DeclaredTimeoutMs is null && !plan.SurfacesOutput)
        {
            ScriptExecutionService.RunFireAndForget(plan);
            return CommandResult.ShowToast("Script completed");
        }

        // Wait so the declared output mode can be honored, falling back to a default
        // timeout when the script didn't specify one.
        var result = ScriptExecutionService.RunAndWait(plan);

        // Failures (couldn't start, timed out, non-zero exit) surface as a dialog whose
        // "View details" opens the full failure report — a toast is too small and too
        // short-lived to explain what went wrong.
        if (result == null || result.TimedOut || result.ExitCode != 0)
            return ScriptFailurePresenter.ToCommandResult(path, plan.Host, "", result);

        // Elevated scripts can't have their output captured, so suppress output handling.
        var output = plan.CaptureOutput ? result.StandardOutput : null;

        return ScriptOutputHandler.ToResult(
            _manifest.Output,
            output,
            _manifest.FileExtension,
            Path.GetFileNameWithoutExtension(path));
    }
}

using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System.IO;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Runs a no-parameter ambient (fire-and-forget) script: any waited output mode whose payoff is
/// external or nil — Toast, Clipboard, Open, File, or None with a declared timeout. It runs the
/// script (bounded by the plan timeout), performs the declared side effect, and returns a
/// <see cref="CommandResult.ShowToast(string)"/> that dismisses the palette with the outcome — see
/// <see cref="AmbientRunner"/> for why the run is synchronous.
///
/// Display modes (Result/Markdown/List) keep the palette open on their own pages instead; the pure
/// None + no-timeout case still uses <see cref="RunScriptCommand"/> (nothing to wait for).
/// </summary>
internal sealed partial class AmbientRunCommand(string path, ScriptManifest manifest, ScriptExecutionPlan plan)
    : InvokableCommand
{
    public override string Name => $"Run {Path.GetFileNameWithoutExtension(path)}";
    public override IconInfo Icon => new(manifest.IconGlyph ?? ""); // Play; or the script's own glyph

    public override CommandResult Invoke()
    {
        // Destructive scripts gate behind a confirmation dialog; only the dialog's primary command
        // actually runs the script.
        if (!string.IsNullOrWhiteSpace(manifest.ConfirmMessage))
        {
            var scriptName = Path.GetFileNameWithoutExtension(path);
            return CommandResult.Confirm(new ConfirmationArgs
            {
                Title = $"Run {scriptName}?",
                Description = manifest.ConfirmMessage,
                PrimaryCommand = new CallbackCommand($"Run {scriptName}", RunNow),
                IsPrimaryCommandCritical = true,
            });
        }

        return RunNow();
    }

    private CommandResult RunNow()
        => AmbientRunner.RunAndToast(plan, manifest);
}

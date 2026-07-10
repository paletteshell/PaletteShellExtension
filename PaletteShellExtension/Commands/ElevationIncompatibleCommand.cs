using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Stands in for a script's normal command when it declares elevation together with a
/// captured-output mode — an impossible combination (elevated processes can't redirect
/// stdout). Selecting the row explains why instead of silently running the script unelevated.
/// </summary>
internal sealed partial class ElevationIncompatibleCommand : InvokableCommand
{
    public override string Name => "Can't run elevated with output";
    public override IconInfo Icon => new(""); // Warning

    // A toast truncated the message; show the full reason in a dialog the user can read.
    public override CommandResult Invoke() =>
        WarningDialog.Show("Can't run elevated with output", ScriptElevation.IncompatibleReason());
}

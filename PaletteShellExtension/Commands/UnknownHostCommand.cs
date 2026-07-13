using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Stands in for a script's normal command when its <c>[ScriptHost(...)]</c> value isn't a
/// recognized interpreter (not auto/pwsh/powershell). Selecting the row explains the bad token
/// instead of silently running the script under a fallback shell it never asked for.
/// </summary>
internal sealed partial class UnknownHostCommand(string badHost) : InvokableCommand
{
    public override string Name => "Unknown script host";
    public override IconInfo Icon => new(""); // Warning

    // A toast truncated the message; show the full reason in a dialog the user can read.
    public override CommandResult Invoke() =>
        WarningDialog.Show("Unknown script host",
            $"Unknown script host '{badHost}'. Supported values are 'auto', 'pwsh', and 'powershell'.");
}

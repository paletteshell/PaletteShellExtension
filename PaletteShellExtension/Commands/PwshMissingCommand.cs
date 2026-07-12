using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Stands in for a script's normal command when its effective host is PowerShell 7 (<c>pwsh</c>)
/// but pwsh.exe isn't installed on this machine. Selecting the row explains that pwsh 7 is
/// required and where to get it, instead of silently failing when the script is clicked.
/// </summary>
internal sealed partial class PwshMissingCommand : InvokableCommand
{
    public override string Name => "PowerShell 7 not installed";
    public override IconInfo Icon => new(""); // Warning

    // A toast truncated the message; show the full reason in a dialog the user can read.
    public override CommandResult Invoke() =>
        WarningDialog.Show("PowerShell 7 not installed",
            "This script requires PowerShell 7 (pwsh.exe), which isn't installed on this machine. " +
            "Install it from https://aka.ms/powershell, or change the script host to 'auto' to run " +
            "under Windows PowerShell instead.");
}

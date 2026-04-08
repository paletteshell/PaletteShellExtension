using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Diagnostics;

namespace PaletteShellExtension.Commands;

internal sealed partial class OpenFolderCommand(string folder) : InvokableCommand
{
    public override string Name => "Open scripts folder";
    public override IconInfo Icon => new("\uE8B7");
    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        return CommandResult.Dismiss();
    }
}

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PaletteShellExtension.Commands;

internal sealed partial class ReloadPageCommand(PaletteShellExtensionPage page) : InvokableCommand
{
    public override string Name => "Reload scripts";
    public override IconInfo Icon => new(""); // Refresh
    public override CommandResult Invoke()
    {
        page.RefreshFiles();
        return CommandResult.KeepOpen();
    }
}

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PaletteShellExtension.Commands;

internal sealed partial class ReloadPageCommand(PaletteShellExtensionPage page) : InvokableCommand
{
    public override string Name => "Reload scripts";
    public override IconInfo Icon => new(""); // Refresh
    public override CommandResult Invoke()
    {
        var count = page.RefreshFiles();
        var noun = count == 1 ? "script" : "scripts";
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = $"Reloaded {count} {noun}",
            Result = CommandResult.KeepOpen(),
        });
    }
}

using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;

namespace PaletteShellExtension.Commands;

internal sealed partial class ReloadPageCommand(PaletteShellExtensionPage page) : InvokableCommand
{
    public override string Name => "Reload scripts";
    public override IconInfo Icon => new(""); // Refresh
    public override CommandResult Invoke()
    {
        // Explicit reload is the one action that forces a fresh pwsh probe: a user who just
        // installed pwsh 7 and hits Reload sees pwsh-pinned scripts go runnable immediately,
        // instead of waiting out the negative re-probe interval.
        ScriptRunner.InvalidatePwshProbe();
        var count = page.RefreshFiles();
        var noun = count == 1 ? "script" : "scripts";
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = $"Reloaded {count} {noun}",
            Result = CommandResult.KeepOpen(),
        });
    }
}

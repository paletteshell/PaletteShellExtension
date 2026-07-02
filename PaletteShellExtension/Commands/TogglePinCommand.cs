using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Pins or unpins a script so it sorts to the top of the list, then invokes
/// <paramref name="onToggled"/> so the owning page can refresh. The label and glyph reflect
/// the state captured when the row was built, so the menu reads "Pin to top" or "Unpin"
/// as appropriate. No confirmation - pinning is trivially reversible.
/// </summary>
internal sealed partial class TogglePinCommand(string path, PinnedScripts pins, Action onToggled) : InvokableCommand
{
    private readonly bool _wasPinned = pins.IsPinned(path);

    public override string Name => _wasPinned ? "Unpin" : "Pin to top";

    // Segoe MDL2: Unpin (E77A) / Pin (E718). Kept as escapes so the source stays ASCII.
    public override IconInfo Icon => _wasPinned ? new("\uE77A") : new("\uE718");

    public override CommandResult Invoke()
    {
        var nowPinned = pins.Toggle(path);
        onToggled();
        return CommandResult.ShowToast(nowPinned ? "Pinned to top" : "Unpinned");
    }
}

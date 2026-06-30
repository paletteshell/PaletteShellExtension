using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Diagnostics;

namespace PaletteShellExtension.Commands;

internal sealed partial class OpenLinkCommand(string name, string url, string iconGlyph) : InvokableCommand
{
    public override string Name => name;
    public override IconInfo Icon => new(iconGlyph);

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return CommandResult.Dismiss();
    }
}

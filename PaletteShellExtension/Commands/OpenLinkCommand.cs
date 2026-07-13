using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
using System.Diagnostics;

namespace PaletteShellExtension.Commands;

internal sealed partial class OpenLinkCommand(string name, string url, string iconGlyph) : InvokableCommand
{
    public override string Name => name;
    public override IconInfo Icon => new(iconGlyph);

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to open link '{url}': {ex.Message}");
            return CommandResult.ShowToast($"Couldn't open link: {ex.Message}");
        }
    }
}

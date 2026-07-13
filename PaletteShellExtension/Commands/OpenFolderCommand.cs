using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
using System.Diagnostics;

namespace PaletteShellExtension.Commands;

internal sealed partial class OpenFolderCommand(string folder, string name = "Open scripts folder") : InvokableCommand
{
    public override string Name => name;
    public override IconInfo Icon => new("\uE8B7");
    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to open folder '{folder}': {ex.Message}");
            return CommandResult.ShowToast($"Couldn't open folder: {ex.Message}");
        }
    }
}

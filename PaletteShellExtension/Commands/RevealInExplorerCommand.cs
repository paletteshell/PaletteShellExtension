using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Diagnostics;
using System.IO;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Opens File Explorer with the script file selected, so the user can manage it without
/// hunting for it in the scripts folder.
/// </summary>
internal sealed partial class RevealInExplorerCommand(string path) : InvokableCommand
{
    public override string Name => "Reveal in File Explorer";
    public override IconInfo Icon => new(""); // Folder

    public override CommandResult Invoke()
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }

        return CommandResult.Dismiss();
    }
}

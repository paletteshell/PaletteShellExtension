using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System.IO;

namespace PaletteShellExtension.Commands;

internal sealed partial class OpenInEditorCommand(string path) : InvokableCommand
{
    public override string Name => "Open in editor";
    public override IconInfo Icon => new(""); // Edit (pencil)

    public override CommandResult Invoke()
    {
        if (File.Exists(path))
        {
            EditorLauncher.Open(path);
        }
        return CommandResult.Dismiss();
    }
}

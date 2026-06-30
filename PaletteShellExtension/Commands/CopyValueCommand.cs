using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Copies a fixed string to the clipboard. Used by List-mode result items so picking
/// an item (a line of stdout / a parsed object) copies its value.
/// </summary>
internal sealed partial class CopyValueCommand(string text) : InvokableCommand
{
    public override string Name => "Copy";
    public override IconInfo Icon => new(""); // Copy

    public override CommandResult Invoke()
    {
        try
        {
            TextCopy.ClipboardService.SetText(text ?? "");
        }
        catch (Exception)
        {
            // Clipboard access can fail; ignore and still report completion.
        }

        return CommandResult.ShowToast("Copied to clipboard");
    }
}

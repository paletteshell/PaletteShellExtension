using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using ClipboardHelper = PaletteShellExtension.Classes.ClipboardHelper;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Copies a fixed string to the clipboard. Used by List-mode result items so picking
/// an item (a line of stdout / a parsed object) copies its value.
/// </summary>
internal sealed partial class CopyValueCommand(string text, string name = "Copy") : InvokableCommand
{
    public override string Name => name;
    public override IconInfo Icon => new(""); // Copy

    public override CommandResult Invoke()
    {
        try
        {
            ClipboardHelper.SetText(text);
        }
        catch (Exception)
        {
            // Clipboard access can fail; ignore and still report completion.
        }

        return CommandResult.ShowToast("Copied to clipboard");
    }
}

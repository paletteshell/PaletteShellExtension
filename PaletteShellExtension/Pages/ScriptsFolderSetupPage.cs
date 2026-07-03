using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Forms;
using System;

namespace PaletteShellExtension.Pages;

internal sealed partial class ScriptsFolderSetupPage : ContentPage
{
    private readonly ScriptsFolderSetupForm _form;

    public ScriptsFolderSetupPage(string suggestedFolder, Action<string> onConfigured)
    {
        Title = "Set up PaletteShell";
        Name = "setup";
        Icon = new(""); // Folder, matches OpenFolderCommand's icon
        Id = "ScriptsFolderSetup";
        _form = new(suggestedFolder, onConfigured);
    }

    public override IContent[] GetContent() => [_form];
}

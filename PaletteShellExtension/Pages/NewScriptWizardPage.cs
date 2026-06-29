using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Forms;


namespace PaletteShellExtension.Pages;

internal sealed partial class NewScriptWizardPage : ContentPage
{
    private readonly NewScriptWizardForm newScriptWizardForm;
    private readonly string _root;

    public NewScriptWizardPage(string root)
    {
        _root = root;
        Title = "New PowerShell script";
        Name = "new";
        Icon = new("\uE710");
        Id = "NewScriptWizard";
        newScriptWizardForm = new(root);
    }

    public override IContent[] GetContent() => [newScriptWizardForm];
}


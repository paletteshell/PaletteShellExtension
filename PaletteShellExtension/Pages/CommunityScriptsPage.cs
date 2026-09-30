using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Commands;

namespace PaletteShellExtension.Pages;

/// <summary>
/// Shown from the main page's "Browse community scripts" entry when the Script Manager app
/// isn't installed. Rather than presuming the user wants to install it (and opening the Store
/// unprompted), this offers an explicit choice: install the app from the Microsoft Store, or
/// just browse the community scripts on GitHub. When the app *is* installed, the main page
/// launches it directly and this page is never reached.
/// </summary>
internal sealed partial class CommunityScriptsPage : ListPage
{
    private const string CommunityScriptsRepositoryUrl = "https://github.com/paletteshell/PaletteShellScripts";

    public CommunityScriptsPage()
    {
        Title = "Browse community scripts";
        Name = "Browse community scripts";
        Icon = new("☁");
        Id = "CommunityScripts";
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new InstallScriptManagerCommand())
        {
            Title = "Install PaletteShell Script Manager",
            Subtitle = "Open the Microsoft Store to install the app for browsing and installing community scripts",
            Icon = new IconInfo("☁"),
        },
        new ListItem(new OpenLinkCommand("Browse on GitHub", CommunityScriptsRepositoryUrl, ""))
        {
            Title = "Browse community scripts on GitHub",
            Subtitle = CommunityScriptsRepositoryUrl,
            Icon = new IconInfo(""), // Link
        },
    ];
}

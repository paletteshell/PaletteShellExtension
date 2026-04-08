using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using System.Collections.Generic;

namespace PaletteShellExtension.Pages;

internal sealed class ScriptParameterFormPage : ContentPage
{
    private readonly ScriptParameterForm _form;

    public ScriptParameterFormPage(
        string scriptPath,
        ScriptManifest manifest,
        string? host = null,
        string? cwd = null,
        Dictionary<string, string>? env = null)
    {
        _form = new ScriptParameterForm(scriptPath, manifest, host, cwd, env);
        
        Title = manifest.Title ?? "Run Script";
        Name = "script-params";
        Icon = new(manifest.IconGlyph ?? "\uE7C3");
        Id = $"ScriptParams_{System.IO.Path.GetFileNameWithoutExtension(scriptPath)}";
    }

    public override IContent[] GetContent() => [_form];
}

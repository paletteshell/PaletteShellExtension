using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using System.Collections.Generic;

namespace PaletteShellExtension.Pages;

internal sealed partial class ScriptParameterFormPage : ContentPage
{
    private readonly ScriptParameterForm _form;
    private readonly MarkdownContent _markdown = new();
    private IContent[] _content;

    public ScriptParameterFormPage(
        string scriptPath,
        ScriptManifest manifest,
        string? host = null,
        string? cwd = null,
        Dictionary<string, string>? env = null)
    {
        _form = new ScriptParameterForm(scriptPath, manifest, host, cwd, env, ShowMarkdown);
        _content = [_form];

        Title = manifest.Title ?? "Run Script";
        Name = "script-params";
        Icon = new(manifest.IconGlyph ?? "\uE7C3");
        Id = $"ScriptParams_{System.IO.Path.GetFileNameWithoutExtension(scriptPath)}";
    }

    // Called by the form (when the script declares ScriptOutput("Markdown")) to
    // replace the input form with the rendered script output.
    private void ShowMarkdown(string body)
    {
        _markdown.Body = string.IsNullOrWhiteSpace(body)
            ? "_Script completed with no output._"
            : body;
        _content = [_markdown];
        RaiseItemsChanged();
    }

    public override IContent[] GetContent() => _content;
}

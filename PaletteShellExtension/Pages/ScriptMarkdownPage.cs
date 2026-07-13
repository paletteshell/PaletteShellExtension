using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PaletteShellExtension.Pages;

// Runs a script and renders its captured stdout as Markdown.
// Used when a script declares ScriptOutput("Markdown").
internal sealed partial class ScriptMarkdownPage : ContentPage
{
    private readonly ScriptExecutionPlan _plan;
    private readonly string _args;
    private readonly string _scriptPath;

    private readonly MarkdownContent _content = new();
    private IContent[] _currentContent;

    // A script run is in flight; ignore re-entrant content fetches while it is.
    private bool _running;

    public ScriptMarkdownPage(
        string scriptPath,
        ScriptManifest manifest,
        ScriptExecutionPlan plan,
        string args = "")
    {
        _scriptPath = scriptPath;
        _plan = plan;
        _args = args;
        _currentContent = [_content];

        Title = manifest.Title ?? Path.GetFileNameWithoutExtension(scriptPath);
        Name = "Run";
        Icon = new(manifest.IconGlyph ?? "");
        Id = $"ScriptMarkdown_{Path.GetFileNameWithoutExtension(scriptPath)}";
        IsLoading = true;
    }

    public override IContent[] GetContent()
    {
        // The host fetches content each time the page is shown. Re-run the script on every
        // navigation so the preview reflects current state instead of the first run's cached
        // output. The in-flight guard keeps an incidental duplicate fetch from launching a
        // second run; the result reaches the UI when MarkdownContent.Body changes raise
        // PropChanged, so there's no need to raise ItemsChanged (doing so leaves the palette
        // stuck showing its loading animation after the content has already updated).
        if (!_running)
        {
            _running = true;
            IsLoading = true;
            _ = Task.Run(RunAndRender);
        }

        return _currentContent;
    }

    private async Task RunAndRender()
    {
        try
        {
            // Elevated scripts can't have their output captured, so an elevated script never
            // reaches Markdown mode (the compatibility gate blocks it) — the plan's RequiresAdmin
            // is false here. Awaited rather than blocked on so the run doesn't pin a threadpool thread.
            var result = await ScriptExecutionService.RunAsync(_plan, _args);

            SetContent(FormatResult(result));
        }
        catch (Exception ex)
        {
            SetContent(new ScriptFailureForm(_scriptPath, _plan.Host, _args, ex));
        }
        finally
        {
            IsLoading = false;
            _running = false;
        }
    }

    private IContent FormatResult(ScriptRunner.ScriptResult? result)
    {
        if (result is null || result.TimedOut || result.ExitCode != 0)
            return new ScriptFailureForm(_scriptPath, _plan.Host, _args, result);

        _content.Body = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? "_Script completed with no output._"
            : result.StandardOutput!;
        return _content;
    }

    private void SetContent(IContent content)
    {
        _currentContent = [content];
        RaiseItemsChanged();
    }
}

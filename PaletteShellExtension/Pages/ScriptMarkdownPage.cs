using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
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

    private readonly MarkdownContent _content = new();

    // A script run is in flight; ignore re-entrant content fetches while it is.
    private bool _running;

    public ScriptMarkdownPage(
        string scriptPath,
        ScriptManifest manifest,
        ScriptExecutionPlan plan,
        string args = "")
    {
        _plan = plan;
        _args = args;

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

        return [_content];
    }

    private async Task RunAndRender()
    {
        try
        {
            // Elevated scripts can't have their output captured, so an elevated script never
            // reaches Markdown mode (the compatibility gate blocks it) — the plan's RequiresAdmin
            // is false here. Awaited rather than blocked on so the run doesn't pin a threadpool thread.
            var result = await ScriptExecutionService.RunAsync(_plan, _args);

            _content.Body = FormatResult(result);
        }
        catch (Exception ex)
        {
            _content.Body = $"**Error running script**\n\n```\n{ex.Message}\n```";
        }
        finally
        {
            IsLoading = false;
            _running = false;
        }
    }

    private static string FormatResult(ScriptRunner.ScriptResult? result)
    {
        if (result is null)
            return "_Failed to start script._";

        if (result.TimedOut)
            return "_Script timed out._";

        if (result.ExitCode != 0)
        {
            var error = result.StandardError?.Trim();
            return string.IsNullOrEmpty(error)
                ? $"**Script failed with exit code {result.ExitCode}.**"
                : $"**Script failed with exit code {result.ExitCode}.**\n\n```\n{error}\n```";
        }

        return string.IsNullOrWhiteSpace(result.StandardOutput)
            ? "_Script completed with no output._"
            : result.StandardOutput!;
    }
}

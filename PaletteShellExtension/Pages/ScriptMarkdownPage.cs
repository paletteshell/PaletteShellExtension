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
    private readonly string _scriptPath;
    private readonly ScriptManifest _manifest;
    private readonly string _host;
    private readonly string? _cwd;
    private readonly Dictionary<string, string> _env;
    private readonly string _args;

    private readonly MarkdownContent _content = new();
    private bool _started;

    public ScriptMarkdownPage(
        string scriptPath,
        ScriptManifest manifest,
        string? host = null,
        string? cwd = null,
        Dictionary<string, string>? env = null,
        string args = "")
    {
        _scriptPath = scriptPath;
        _manifest = manifest;
        _host = host ?? manifest.Host ?? "pwsh";
        _cwd = cwd;
        _env = env ?? new(StringComparer.OrdinalIgnoreCase);
        _args = args;

        Title = manifest.Title ?? Path.GetFileNameWithoutExtension(scriptPath);
        Name = "Run";
        Icon = new(manifest.IconGlyph ?? "");
        Id = $"ScriptMarkdown_{Path.GetFileNameWithoutExtension(scriptPath)}";
        IsLoading = true;
    }

    public override IContent[] GetContent()
    {
        // Kick off the script the first time the host asks for content; refresh when done.
        if (!_started)
        {
            _started = true;
            _ = Task.Run(RunAndRender);
        }

        return [_content];
    }

    private void RunAndRender()
    {
        try
        {
            var timeout = _manifest.TimeoutMs is > 0 ? _manifest.TimeoutMs!.Value : 30000;

            // Elevated scripts can't have their output captured, so Markdown mode
            // always runs unelevated to be able to render the result.
            var result = ScriptRunner.RunScriptAndWait(
                scriptPath: _scriptPath,
                args: _args,
                host: _host,
                cwd: _cwd,
                env: _env,
                requiresAdmin: false,
                timeoutMs: timeout);

            _content.Body = FormatResult(result);
        }
        catch (Exception ex)
        {
            _content.Body = $"**Error running script**\n\n```\n{ex.Message}\n```";
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
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

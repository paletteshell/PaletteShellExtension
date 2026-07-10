using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PaletteShellExtension.Pages;

/// <summary>
/// Runs a script and presents its output as a single result you can copy — the way a
/// calculator shows an answer. Used when a script declares <c>[ScriptOutput('Result')]</c>.
///
/// Selecting the result copies it to the clipboard (Enter), and a secondary "Run again"
/// command re-runs the script in place — handy for generators such as a new GUID, password,
/// or token, where you want to peek at the value and grab a fresh one without leaving.
/// </summary>
internal sealed partial class ScriptResultPage : ListPage
{
    private readonly string _scriptPath;
    private readonly ScriptManifest _manifest;
    private readonly string _host;
    private readonly string? _cwd;
    private readonly Dictionary<string, string> _env;

    private IListItem[] _items = [];
    private bool _started;

    // Set while a run is in flight so an incidental duplicate GetItems (or a rapid second
    // "Run again") doesn't launch a second overlapping process.
    private bool _running;

    public ScriptResultPage(
        string scriptPath,
        ScriptManifest manifest,
        string? host = null,
        string? cwd = null,
        Dictionary<string, string>? env = null)
    {
        _scriptPath = scriptPath;
        _manifest = manifest;
        _host = host ?? manifest.Host ?? PaletteShellSettingsManager.Instance.DefaultHost;
        _cwd = cwd;
        _env = env ?? new(StringComparer.OrdinalIgnoreCase);

        Title = manifest.Title ?? Path.GetFileNameWithoutExtension(scriptPath);
        Name = "Run";
        Icon = new(manifest.IconGlyph ?? "");
        Id = $"ScriptResult_{Path.GetFileNameWithoutExtension(scriptPath)}";
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        // Kick off the first run the first time the host asks for items.
        if (!_started)
        {
            _started = true;
            Run();
        }

        return _items;
    }

    /// <summary>Runs (or re-runs) the script and refreshes the result. Exposed via the
    /// "Run again" command so generators can produce a fresh value without leaving the page.</summary>
    private void Run()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        IsLoading = true;
        _ = Task.Run(Execute);
    }

    private async Task Execute()
    {
        try
        {
            var timeout = _manifest.TimeoutMs is > 0 ? _manifest.TimeoutMs!.Value : PaletteShellSettingsManager.Instance.DefaultTimeoutMs;

            // Elevated scripts can't have their output captured, so Result mode always runs
            // unelevated — there'd be no value to show otherwise. Awaited rather than
            // blocked on so the script's run time doesn't pin a threadpool thread.
            var result = await ScriptRunner.RunScriptAndWaitAsync(
                scriptPath: _scriptPath,
                args: "",
                host: _host,
                cwd: _cwd,
                env: _env,
                requiresAdmin: false,
                timeoutMs: timeout,
                requiredModules: _manifest.RequiredModules);

            _items = BuildItems(result);
        }
        catch (Exception ex)
        {
            _items = [Message($"Error running script: {ex.Message}")];
        }
        finally
        {
            _running = false;
            IsLoading = false;
            RaiseItemsChanged();
        }
    }

    private IListItem[] BuildItems(ScriptRunner.ScriptResult? result)
    {
        // Failures get an actionable row (Enter opens the full failure report) instead of
        // an inert message, so the user can see the whole error rather than a summary.
        if (result is null || result.TimedOut || result.ExitCode != 0)
            return [ScriptFailurePresenter.ToListItem(_scriptPath, _host, "", result)];

        var value = result.StandardOutput?.Trim();
        if (string.IsNullOrEmpty(value))
            return [Message("Script produced no output.")];

        return [BuildResultItem(value)];
    }

    private ListItem BuildResultItem(string value)
    {
        return new ListItem(new CopyValueCommand(value))
        {
            Title = value,
            Subtitle = "Press Enter to copy to the clipboard",
            Icon = new IconInfo(_manifest.IconGlyph ?? ""),
            MoreCommands =
            [
                // Re-run in place so generators (GUID, password, token, …) can produce a fresh
                // value; KeepOpen leaves the palette on this page while the new run refreshes it.
                new CommandContextItem(new CallbackCommand("Run again", () =>
                {
                    Run();
                    return CommandResult.KeepOpen();
                })),
            ],
        };
    }

    private static ListItem Message(string text)
        => new(new NoOpCommand()) { Title = text };
}

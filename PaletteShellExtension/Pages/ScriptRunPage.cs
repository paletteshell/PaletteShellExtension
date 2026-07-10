using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Commands;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PaletteShellExtension.Pages;

/// <summary>
/// Common asynchronous execution page for every "waited" no-parameter output mode — Toast,
/// Clipboard, Open, File, and None with a declared timeout. The synchronous <c>Invoke()</c> runner
/// blocks the thread the host is making its COM call on for the whole run (up to ten minutes),
/// which freezes the palette. This page instead navigates immediately, shows a progress spinner,
/// runs the script on a background thread via <see cref="ScriptExecutionService.RunAsync"/>, and
/// dispatches the clipboard/toast/file/open/result behavior when it finishes. Navigating away
/// disposes the page, which cancels the run and kills the child process.
/// </summary>
internal sealed partial class ScriptRunPage : ListPage, IDisposable
{
    private readonly string _scriptPath;
    private readonly ScriptManifest _manifest;
    private readonly ScriptExecutionPlan _plan;

    private IListItem[] _items = [];
    private bool _started;

    // Set while a run is in flight so an incidental duplicate GetItems doesn't launch a second run.
    private bool _running;

    // Set once a destructive script's confirmation has been accepted, so the run starts only then.
    private bool _confirmed;

    // Cancelled (and the child process killed) when the page is disposed on navigate-away.
    private readonly CancellationTokenSource _cts = new();

    public ScriptRunPage(string scriptPath, ScriptManifest manifest, ScriptExecutionPlan plan)
    {
        _scriptPath = scriptPath;
        _manifest = manifest;
        _plan = plan;

        Title = manifest.Title ?? Path.GetFileNameWithoutExtension(scriptPath);
        Name = "Run";
        Icon = new(manifest.IconGlyph ?? "");
        Id = $"ScriptRun_{Path.GetFileNameWithoutExtension(scriptPath)}";
        IsLoading = true;
    }

    private bool NeedsConfirmation => !string.IsNullOrWhiteSpace(_manifest.ConfirmMessage);

    public override IListItem[] GetItems()
    {
        if (!_started)
        {
            _started = true;

            // Destructive scripts gate behind a confirmation dialog first; the run only starts once
            // the user accepts. Everything else starts immediately on first fetch.
            if (NeedsConfirmation && !_confirmed)
            {
                IsLoading = false;
                _items = [BuildConfirmItem()];
            }
            else
            {
                Start();
            }
        }

        return _items;
    }

    // A single row that surfaces the destructive-run confirmation. Enter shows the native confirm
    // dialog; accepting it starts the run in place.
    private ListItem BuildConfirmItem()
    {
        var scriptName = Path.GetFileNameWithoutExtension(_scriptPath);
        return new ListItem(new CallbackCommand($"Run {scriptName}", () => CommandResult.Confirm(new ConfirmationArgs
        {
            Title = $"Run {scriptName}?",
            Description = _manifest.ConfirmMessage ?? "",
            PrimaryCommand = new CallbackCommand($"Run {scriptName}", () =>
            {
                _confirmed = true;
                Start();
                return CommandResult.KeepOpen();
            }),
            IsPrimaryCommandCritical = true,
        })))
        {
            Title = $"Run {scriptName}?",
            Subtitle = _manifest.ConfirmMessage ?? "",
            Icon = new IconInfo(_manifest.IconGlyph ?? ""),
        };
    }

    private void Start()
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
            var result = await ScriptExecutionService.RunAsync(_plan, cancellationToken: _cts.Token);
            _items = BuildItems(result);
        }
        catch (OperationCanceledException)
        {
            // The page was navigated away from mid-run; the child was killed. Nothing to render.
            return;
        }
        catch (Exception ex)
        {
            _items = [Message($"Error running script: {ex.Message}")];
        }
        finally
        {
            _running = false;
            IsLoading = false;
            if (!_cts.IsCancellationRequested)
            {
                RaiseItemsChanged();
            }
        }
    }

    private IListItem[] BuildItems(ScriptRunner.ScriptResult? result)
    {
        // Failures get an actionable row (Enter opens the full failure report) instead of an inert
        // message, so the user can see the whole error rather than a summary.
        if (result is null || result.TimedOut || result.ExitCode != 0)
        {
            return [ScriptFailurePresenter.ToListItem(_scriptPath, _plan.Host, "", result)];
        }

        // Elevated runs can't capture output, so there's nothing to surface beyond completion.
        var output = _plan.CaptureOutput ? result.StandardOutput : null;
        var outcome = ScriptRunDispatcher.Apply(_manifest, output, Path.GetFileNameWithoutExtension(_scriptPath));

        // A non-web, non-file open target: confirm the exact target before letting Windows launch
        // whatever handler it resolves to.
        if (outcome.UnsafeOpenTarget is { } target)
        {
            return
            [
                new ListItem(new CallbackCommand("Open", () =>
                {
                    ScriptOutputHandler.OpenTarget(target);
                    return CommandResult.KeepOpen();
                }))
                {
                    Title = target,
                    Subtitle = "Not a web link or a file on disk — press Enter to open it",
                    Icon = new IconInfo(""), // Warning
                },
            ];
        }

        // Clipboard/Toast leave something worth copying again; show it as a copyable result row.
        if (outcome.CopyValue is { } value)
        {
            return
            [
                new ListItem(new CopyValueCommand(value))
                {
                    Title = outcome.Status,
                    Subtitle = "Press Enter to copy to the clipboard",
                    Icon = new IconInfo(_manifest.IconGlyph ?? ""),
                },
            ];
        }

        return [Message(outcome.Status)];
    }

    private static ListItem Message(string text) => new(new NoOpCommand()) { Title = text };

    public void Dispose()
    {
        try
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Already disposed — nothing to cancel.
        }

        _cts.Dispose();
    }
}

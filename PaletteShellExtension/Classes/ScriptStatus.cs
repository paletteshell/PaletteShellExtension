using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Shows a transient "Running &lt;script&gt;…" spinner in Command Palette's status bar while a
/// script executes (via <see cref="ExtensionHost"/>), then clears it. Folded into the runner so
/// every output mode benefits — a slow script no longer looks frozen until its toast/page appears.
///
/// Operations run through a single serialized chain (<see cref="Enqueue"/>) and each host call is
/// awaited. This matters: <c>ExtensionHost.ShowStatus</c>/<c>HideStatus</c> are fire-and-forget on
/// the thread pool with no ordering guarantee, so for a quick script the show and its matching hide
/// can otherwise reach the host out of order and leave the spinner stuck. Every call is best-effort:
/// if the host hasn't wired up a status sink they quietly no-op.
/// </summary>
internal static class ScriptStatus
{
    // Guards the continuation chain below.
    private static readonly object _gate = new();
    private static Task _chain = Task.CompletedTask;

    /// <summary>Shows an indeterminate-progress "Running <paramref name="scriptName"/>…" banner and
    /// returns a handle to pass back to <see cref="Hide"/> once the run finishes.</summary>
    public static IStatusMessage ShowRunning(string scriptName)
    {
        var message = new StatusMessage
        {
            Message = $"Running {scriptName}…",
            State = MessageState.Info,
            Progress = new ProgressState { IsIndeterminate = true },
        };

        Enqueue(host => host.ShowStatus(message, StatusContext.Extension));
        return message;
    }

    public static void Hide(IStatusMessage? message)
    {
        if (message is null)
        {
            return;
        }

        Enqueue(host => host.HideStatus(message));
    }

    /// <summary>Appends a host call to the serialized chain so it runs after all prior ones complete.</summary>
    private static void Enqueue(Func<IExtensionHost, IAsyncAction> operation)
    {
        lock (_gate)
        {
            _chain = RunAfter(_chain, operation);
        }
    }

    private static async Task RunAfter(Task previous, Func<IExtensionHost, IAsyncAction> operation)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A prior status op failing must not stall the rest of the chain.
        }

        var host = ExtensionHost.Host;
        if (host is null)
        {
            return;
        }

        try
        {
            await operation(host).AsTask().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The host may not support status, or was torn down; the spinner is optional.
        }
    }
}

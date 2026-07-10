using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System.IO;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Shared presentation for script failures. A transient toast gave the user nothing to act
/// on — truncated stderr that vanished before it could be read or copied. Instead, failures
/// surface as a confirmation dialog (or an actionable list row on the List/Result pages)
/// whose primary action opens the full failure report in the user's editor. Centralized here
/// so the ConfirmationArgs wiring isn't duplicated across the run paths.
/// </summary>
internal static class ScriptFailurePresenter
{
    /// <summary>
    /// Failure result for toast-based run paths: a dialog with the short summary whose
    /// "View details" command opens the full report via <see cref="EditorLauncher.OpenContent"/>.
    /// </summary>
    public static CommandResult ToCommandResult(string scriptPath, string host, string args, ScriptRunner.ScriptResult? result)
    {
        var name = Path.GetFileNameWithoutExtension(scriptPath);
        return CommandResult.Confirm(new ConfirmationArgs
        {
            Title = $"{name} {ScriptFailureReport.DescribeOutcome(result)}",
            Description = result is null
                ? "The script process could not be started."
                : ScriptRunner.DescribeFailure(result),
            PrimaryCommand = new CallbackCommand("View details", () =>
            {
                OpenReport(scriptPath, host, args, result);
                return CommandResult.Dismiss();
            }),
            IsPrimaryCommandCritical = false,
        });
    }

    /// <summary>
    /// Failure row for list-shaped pages: Enter opens the full report (keeping the page
    /// open), and the context menu offers the log folder.
    /// </summary>
    public static ListItem ToListItem(string scriptPath, string host, string args, ScriptRunner.ScriptResult? result)
    {
        return new ListItem(new CallbackCommand("View details", () =>
        {
            OpenReport(scriptPath, host, args, result);
            return CommandResult.KeepOpen();
        }))
        {
            Title = result is null ? "Failed to start script." : ScriptRunner.DescribeFailure(result),
            Subtitle = "Press Enter to open the full failure report",
            Icon = new IconInfo(""), // Warning
            MoreCommands =
            [
                new CommandContextItem(new OpenFolderCommand(EditorLauncher.OutputDirectory, "Open report folder")),
                new CommandContextItem(new OpenFolderCommand(Log.LogDirectory, "Open log folder")),
            ],
        };
    }

    private static void OpenReport(string scriptPath, string host, string args, ScriptRunner.ScriptResult? result)
    {
        var name = Path.GetFileNameWithoutExtension(scriptPath);
        EditorLauncher.OpenContent(
            ScriptFailureReport.Build(scriptPath, host, args, result),
            ".txt",
            $"{name}-failure");
    }
}

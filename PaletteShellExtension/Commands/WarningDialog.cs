using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Shared presentation for the non-runnable warning rows (incompatible version, unknown host,
/// elevation/output conflict). The palette truncates a row's subtitle and a transient toast, so a
/// meaningful reason was lost before the user could read it. This surfaces the full text in a
/// dismissible confirmation dialog instead — the same "show the whole thing" approach
/// <see cref="ScriptFailurePresenter"/> uses for runtime failures.
/// </summary>
internal static class WarningDialog
{
    public static CommandResult Show(string title, string message) =>
        Show(title, message, "OK", () => CommandResult.Dismiss());

    /// <summary>Same dialog with a custom primary action (e.g. "Open to fix" for a malformed
    /// script), so the full reason is readable and the fix is one keypress away.</summary>
    public static CommandResult Show(string title, string message, string primaryLabel, Func<CommandResult> primaryAction) =>
        CommandResult.Confirm(new ConfirmationArgs
        {
            Title = title,
            Description = message,
            PrimaryCommand = new CallbackCommand(primaryLabel, primaryAction),
            IsPrimaryCommandCritical = false,
        });
}

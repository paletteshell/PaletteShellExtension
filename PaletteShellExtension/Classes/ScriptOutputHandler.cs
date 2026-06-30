using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Turns a script's captured stdout into a <see cref="CommandResult"/> according to its
/// declared <c>[ScriptOutput(...)]</c> mode. Markdown is intentionally not handled here —
/// it renders into a page and is routed by the caller before this is reached.
/// </summary>
internal static class ScriptOutputHandler
{
    public static CommandResult ToResult(string? mode, string? output, string? fileExtension = null, string? fileBaseName = null)
    {
        switch (mode?.Trim().ToLowerInvariant())
        {
            case "clipboard":
                if (!string.IsNullOrEmpty(output))
                {
                    TrySetClipboard(output);
                    return CommandResult.ShowToast("Copied to clipboard");
                }
                return CommandResult.ShowToast("Script completed");

            // Write stdout to a temp file and open it in the user's editor. Useful for
            // output that's too large or structured to be readable in a toast.
            case "file":
                if (!string.IsNullOrEmpty(output))
                {
                    EditorLauncher.OpenContent(output, fileExtension, fileBaseName);
                    return CommandResult.ShowToast("Opened output in editor");
                }
                return CommandResult.ShowToast("Script completed");

            // Run silently: confirm completion without surfacing the output.
            case "none":
                return CommandResult.ShowToast("Script completed");

            // "toast" (and any unrecognized value) surfaces the captured output.
            default:
                return !string.IsNullOrEmpty(output)
                    ? CommandResult.ShowToast(output)
                    : CommandResult.ShowToast("Script completed");
        }
    }

    private static void TrySetClipboard(string text)
    {
        try
        {
            TextCopy.ClipboardService.SetText(text ?? "");
        }
        catch (Exception)
        {
            // Clipboard access can fail; ignore and continue.
        }
    }
}

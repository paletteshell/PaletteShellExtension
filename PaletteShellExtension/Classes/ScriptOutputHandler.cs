using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Low-level helpers for acting on a script's captured stdout per its declared
/// <c>[ScriptOutput(...)]</c> mode — set the clipboard, open a target, decide whether a target is
/// safe to open unprompted. The mode dispatch itself lives in <see cref="ScriptRunDispatcher"/>,
/// which runs on the async execution surfaces (never on the host's blocking COM call).
/// </summary>
internal static class ScriptOutputHandler
{
    internal static CommandResult OpenTarget(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return CommandResult.ShowToast("Opened script output");
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to open script output target '{target}': {ex.Message}");
            return CommandResult.ShowToast($"Couldn't open script output: {ex.Message}");
        }
    }

    // Open without prompting only for web links and real files/folders on disk. .lnk
    // shortcuts are excluded even when they exist: launching one runs whatever it points at.
    internal static bool IsSafeOpenTarget(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return File.Exists(target) || Directory.Exists(target);
        }
        catch
        {
            // Malformed path (bad chars, too long) — treat as unsafe and let the confirm show it.
            return false;
        }
    }

    internal static string? GetOpenTarget(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        return output
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim().Trim('"'))
            .FirstOrDefault(line => line.Length > 0);
    }

    internal static void TrySetClipboard(string text)
    {
        try
        {
            ClipboardHelper.SetText(text);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to set clipboard text: {ex.Message}");
            throw;
        }
    }
}

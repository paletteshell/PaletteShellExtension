using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Opens a file in the user's preferred editor: <c>$VISUAL</c>, then <c>$EDITOR</c>,
/// falling back to Notepad when neither is set.
/// </summary>
internal static class EditorLauncher
{
    // Written with a BOM so arbitrary external editors (notably the notepad.exe fallback)
    // reliably detect UTF-8 instead of guessing at all-non-Latin content.
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Temp folder holding failure reports and "File" output-mode results. Exposed so
    /// the palette can offer an "Open report folder" command and so cleanup has one source of truth.</summary>
    internal static string OutputDirectory => Path.Combine(Path.GetTempPath(), "PaletteShell");

    public static void Open(string path)
    {
        var editor = PaletteShellSettingsManager.Instance.PreferredEditor
                 ?? Environment.GetEnvironmentVariable("VISUAL")
                 ?? Environment.GetEnvironmentVariable("EDITOR")
                 ?? "notepad.exe";
        try
        {
            Process.Start(new ProcessStartInfo(editor, $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // A misconfigured $EDITOR/$VISUAL or a missing handler must not throw through a
            // Command Palette invocation. Log and give up quietly — the temp file still exists.
            Log.Warn($"Failed to open '{path}' in editor '{editor}': {ex.Message}");
        }
    }

    /// <summary>
    /// Writes <paramref name="content"/> to a temp file and opens it in the editor.
    /// <paramref name="extension"/> is a hint (e.g. <c>".json"</c>) so the editor applies
    /// the right syntax highlighting; <paramref name="baseName"/> names the file for
    /// readability. Returns the path written.
    /// </summary>
    public static string OpenContent(string content, string? extension = null, string? baseName = null)
    {
        var dir = OutputDirectory;
        Directory.CreateDirectory(dir);
        PruneOld(dir);

        var name = Sanitize(baseName) ?? "output";
        var fileName = $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}{NormalizeExtension(extension)}";
        var path = Path.Combine(dir, fileName);

        File.WriteAllText(path, content ?? "", Utf8WithBom);
        Open(path);
        return path;
    }

    /// <summary>
    /// Deletes files in the temp folder older than the configured retention window — these
    /// reports/outputs can carry sensitive script data, so they shouldn't accumulate forever.
    /// Gated by the user's cleanup setting and entirely best-effort: neither a disabled
    /// setting nor a locked file may break the write that triggered it.
    /// </summary>
    private static void PruneOld(string dir)
    {
        try
        {
            var settings = PaletteShellSettingsManager.Instance;
            if (!settings.CleanupTempEnabled)
            {
                return;
            }

            var cutoff = DateTime.Now.AddDays(-settings.CleanupRetentionDays);
            foreach (var file in Directory.GetFiles(dir))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception)
                {
                    // A locked or already-removed file just gets picked up next time.
                }
            }
        }
        catch (Exception)
        {
            // Cleanup is a convenience; never let it break opening the report/output.
        }
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ".txt";
        var ext = extension.Trim();
        return ext.StartsWith('.') ? ext : "." + ext;
    }

    private static string? Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }
}

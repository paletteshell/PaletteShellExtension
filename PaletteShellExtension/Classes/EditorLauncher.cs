using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Opens a file in the user's preferred editor: <c>$VISUAL</c>, then <c>$EDITOR</c>,
/// falling back to Notepad when neither is set.
/// </summary>
internal static class EditorLauncher
{
    public static void Open(string path)
    {
        var editor = Environment.GetEnvironmentVariable("VISUAL")
                 ?? Environment.GetEnvironmentVariable("EDITOR")
                 ?? "notepad.exe";
        Process.Start(new ProcessStartInfo(editor, $"\"{path}\"") { UseShellExecute = true });
    }

    /// <summary>
    /// Writes <paramref name="content"/> to a temp file and opens it in the editor.
    /// <paramref name="extension"/> is a hint (e.g. <c>".json"</c>) so the editor applies
    /// the right syntax highlighting; <paramref name="baseName"/> names the file for
    /// readability. Returns the path written.
    /// </summary>
    public static string OpenContent(string content, string? extension = null, string? baseName = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "PaletteShell");
        Directory.CreateDirectory(dir);

        var name = Sanitize(baseName) ?? "output";
        var fileName = $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}{NormalizeExtension(extension)}";
        var path = Path.Combine(dir, fileName);

        File.WriteAllText(path, content ?? "");
        Open(path);
        return path;
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

using System;
using System.Diagnostics;

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
}

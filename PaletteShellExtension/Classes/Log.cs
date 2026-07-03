using System;
using System.IO;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Minimal file logger for diagnosing script-loading and execution failures. The extension
/// runs as a COM server hosted by Command Palette, so there is no console the user can see —
/// this is the only way to learn *why* a script failed to parse or run without attaching a
/// debugger. Every write is best-effort; logging must never be the thing that crashes the
/// extension.
/// </summary>
internal static class Log
{
    private static readonly object WriteLock = new();
    private static readonly string LogPath;

    static Log()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaletteShell",
            "logs");

        LogPath = Path.Combine(dir, $"palette-shell-{DateTime.Now:yyyyMMdd}.log");

        try
        {
            Directory.CreateDirectory(dir);
            PruneOldLogs(dir);
        }
        catch (Exception)
        {
            // If we can't even create the log directory, logging is a no-op for this session.
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception}");

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (WriteLock)
            {
                File.AppendAllText(LogPath, line);
            }
        }
        catch (Exception)
        {
            // Never let logging failures surface to the caller.
        }
    }

    // Keeps the log directory from growing forever — one file per day, last week retained.
    private static void PruneOldLogs(string dir)
    {
        var cutoff = DateTime.Now.AddDays(-7);
        foreach (var file in Directory.GetFiles(dir, "palette-shell-*.log"))
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
}

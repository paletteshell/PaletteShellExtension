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

    /// <summary>Where the daily log files live. Exposed so the palette can offer an
    /// "Open log folder" command — otherwise the user has no way to find these files.</summary>
    internal static string LogDirectory { get; }

    // A single append-mode writer held for the process lifetime (re-opened when the day
    // rolls over), so a burst of warnings — e.g. a reload of a folder with several
    // malformed scripts — doesn't pay a full file open/close per line.
    private static StreamWriter? _writer;
    private static string _writerDate = "";

    static Log()
    {
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaletteShell",
            "logs");

        try
        {
            Directory.CreateDirectory(LogDirectory);
            PruneOldLogs(LogDirectory);
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
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            lock (WriteLock)
            {
                GetWriter()?.WriteLine(line);
            }
        }
        catch (Exception)
        {
            // Never let logging failures surface to the caller.
        }
    }

    // Returns the shared writer, opening (or rolling over) the day's file as needed.
    // Must be called under WriteLock. A failed open leaves the writer null and the date
    // marker unset, so the next write retries — same failure-mode behavior as the old
    // open-per-line approach.
    private static StreamWriter? GetWriter()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        if (today == _writerDate)
        {
            return _writer;
        }

        try { _writer?.Dispose(); }
        catch (Exception) { }
        _writer = null;
        _writerDate = "";

        try
        {
            // FileShare.ReadWrite keeps the held handle from locking out another process
            // (or the user tailing the log) the way the old transient appends never did.
            var stream = new FileStream(
                Path.Combine(LogDirectory, $"palette-shell-{today}.log"),
                FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(stream) { AutoFlush = true };
            _writerDate = today;
        }
        catch (Exception)
        {
            // Couldn't open the log file; stay a no-op and retry on the next write.
        }

        return _writer;
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

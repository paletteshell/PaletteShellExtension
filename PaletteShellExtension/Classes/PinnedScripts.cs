using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Tracks which scripts the user has pinned to the top of the list. State is a flat
/// <c>pinned.txt</c> (one script key per line) kept alongside the scripts in the scripts
/// folder, so it survives reloads/restarts and travels if the folder is synced or copied.
/// Plain text keeps this trim-safe and dependency-free — no reflection-based serializer.
///
/// Pinning is a per-user UI preference, not script identity, so it deliberately lives
/// outside the <c>.ps1</c> files — toggling a pin never rewrites the user's script.
/// Scripts are keyed by their path relative to the scripts root (forward-slashed so the key
/// is stable regardless of the OS separator), which keeps two same-named scripts in different
/// subfolders distinct. A rename or move orphans that script's pin, which is harmless. Every
/// operation is best-effort so a locked or corrupt store never breaks the list.
/// </summary>
internal sealed class PinnedScripts
{
    private const string StoreFileName = "pinned.txt";

    private readonly string _rootDirectory;
    private readonly string _storePath;
    private readonly HashSet<string> _pinned;

    public PinnedScripts(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        _storePath = Path.Combine(rootDirectory, StoreFileName);
        _pinned = Load(_storePath);
    }

    /// <summary>True if the script at <paramref name="path"/> is pinned.</summary>
    public bool IsPinned(string path) => _pinned.Contains(KeyFor(path));

    /// <summary>Pins or unpins the script and persists the change. Returns the new pinned state.</summary>
    public bool Toggle(string path)
    {
        var key = KeyFor(path);
        var nowPinned = _pinned.Add(key);
        if (!nowPinned)
        {
            _pinned.Remove(key);
        }

        Save();
        return nowPinned;
    }

    // Path relative to the scripts root, normalized to forward slashes so the stored key
    // doesn't depend on the platform separator (e.g. "Git/Sync.ps1").
    private string KeyFor(string path) =>
        Path.GetRelativePath(_rootDirectory, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    private static HashSet<string> Load(string storePath)
    {
        try
        {
            if (File.Exists(storePath))
            {
                var keys = File.ReadAllLines(storePath)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0);
                return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception)
        {
            // Missing or corrupt store — start with nothing pinned rather than failing the list.
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private void Save()
    {
        try
        {
            var ordered = _pinned.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
            File.WriteAllLines(_storePath, ordered);
        }
        catch (Exception)
        {
            // Best-effort: an in-memory pin still takes effect this session even if it can't persist.
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PaletteShellExtension.Classes;

/// <summary>Where a locally installed script came from in the community catalog, so an
/// update check can compare its recorded sha against the catalog's current one.</summary>
internal sealed record InstalledCommunityScript(string SourcePath, string Sha);

/// <summary>
/// Tracks which local scripts were installed from the community catalog, so the palette can
/// show provenance, detect when a newer version is available (sha mismatch), and avoid
/// clobbering a same-named script the user wrote themselves. State is a small JSON file
/// (<c>community-installed.json</c>) alongside <c>pinned.txt</c> - same best-effort,
/// never-throw philosophy as <see cref="PinnedScripts"/>: a locked or corrupt store never
/// breaks the list, it just starts empty for the session.
/// </summary>
internal sealed class InstalledCommunityScripts
{
    private const string StoreFileName = "community-installed.json";

    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };

    private readonly string _rootDirectory;
    private readonly string _storePath;
    private readonly Dictionary<string, InstalledCommunityScript> _installed;

    public InstalledCommunityScripts(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        _storePath = Path.Combine(rootDirectory, StoreFileName);
        _installed = Load(_storePath);
    }

    public bool TryGetInstalled(string localPath, out InstalledCommunityScript? record) =>
        _installed.TryGetValue(KeyFor(localPath), out record);

    /// <summary>Records (or updates) the install and persists the change.</summary>
    public void Record(string localPath, string sourcePath, string? sha)
    {
        _installed[KeyFor(localPath)] = new InstalledCommunityScript(sourcePath, sha ?? "");
        Save();
    }

    public void Remove(string localPath)
    {
        if (_installed.Remove(KeyFor(localPath)))
        {
            Save();
        }
    }

    // Path relative to the scripts root, normalized to forward slashes - same convention as
    // PinnedScripts so a rename/move simply orphans the record rather than breaking anything.
    private string KeyFor(string path) =>
        Path.GetRelativePath(_rootDirectory, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    private static Dictionary<string, InstalledCommunityScript> Load(string storePath)
    {
        try
        {
            if (File.Exists(storePath))
            {
                var root = JsonNode.Parse(File.ReadAllText(storePath))?.AsObject();
                if (root is not null)
                {
                    var map = new Dictionary<string, InstalledCommunityScript>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, value) in root)
                    {
                        var obj = value?.AsObject();
                        var source = obj?["sourcePath"]?.ToString();
                        if (source is not null)
                        {
                            map[key] = new InstalledCommunityScript(source, obj?["sha"]?.ToString() ?? "");
                        }
                    }

                    return map;
                }
            }
        }
        catch (Exception)
        {
            // Missing or corrupt store - start with nothing tracked rather than failing the list.
        }

        return new Dictionary<string, InstalledCommunityScript>(StringComparer.OrdinalIgnoreCase);
    }

    private void Save()
    {
        try
        {
            var root = new JsonObject();
            foreach (var (key, value) in _installed.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                root[key] = new JsonObject
                {
                    ["sourcePath"] = value.SourcePath,
                    ["sha"] = value.Sha,
                };
            }

            File.WriteAllText(_storePath, root.ToJsonString(SaveOptions));
        }
        catch (Exception)
        {
            // Best-effort: an in-memory record still takes effect this session even if it can't persist.
        }
    }
}

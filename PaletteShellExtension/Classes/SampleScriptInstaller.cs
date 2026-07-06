using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PaletteShellExtension.Classes;

/// <summary>Version and content hash of a bundled sample script as of the last time this
/// process wrote it to the scripts folder, so a later run can tell whether the shipped copy
/// has moved on and whether the user has since edited their copy.</summary>
internal sealed record InstalledSampleScript(string? Version, string ContentHash);

/// <summary>
/// Tracks the version/hash of each bundled sample script this process has written into the
/// scripts folder. State is a small JSON file (<c>sample-scripts.json</c>) alongside
/// <c>pinned.txt</c> and <c>community-installed.json</c> - same best-effort, never-throw
/// philosophy as those: a locked or corrupt store never breaks startup, it just starts empty
/// for the session (which just means samples fall back to "don't overwrite" until it can
/// write again).
/// </summary>
internal sealed class InstalledSampleScripts
{
    private const string StoreFileName = "sample-scripts.json";

    private readonly string _storePath;
    private readonly Dictionary<string, InstalledSampleScript> _installed;

    public InstalledSampleScripts(string rootDirectory)
    {
        _storePath = Path.Combine(rootDirectory, StoreFileName);
        _installed = Load(_storePath);
    }

    public bool TryGet(string fileName, out InstalledSampleScript? record) =>
        _installed.TryGetValue(fileName, out record);

    /// <summary>Records (or updates) the sample's installed version/hash and persists the change.</summary>
    public void Record(string fileName, string? version, string contentHash)
    {
        Record(fileName, version, contentHash, persist: true);
    }

    internal void Record(string fileName, string? version, string contentHash, bool persist)
    {
        _installed[fileName] = new InstalledSampleScript(version, contentHash);
        if (persist)
        {
            Save();
        }
    }

    internal void Save() => SaveCore();

    private static Dictionary<string, InstalledSampleScript> Load(string storePath)
    {
        try
        {
            if (File.Exists(storePath))
            {
                var root = JsonNode.Parse(File.ReadAllText(storePath))?.AsObject();
                if (root is not null)
                {
                    var map = new Dictionary<string, InstalledSampleScript>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, value) in root)
                    {
                        var obj = value?.AsObject();
                        var hash = obj?["hash"]?.ToString();
                        if (hash is not null)
                        {
                            map[key] = new InstalledSampleScript(obj?["version"]?.ToString(), hash);
                        }
                    }

                    return map;
                }
            }
        }
        catch (Exception)
        {
            // Missing or corrupt store - start with nothing tracked rather than failing startup.
        }

        return new Dictionary<string, InstalledSampleScript>(StringComparer.OrdinalIgnoreCase);
    }

    private void SaveCore()
    {
        try
        {
            var root = new JsonObject();
            foreach (var (key, value) in _installed.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                var entry = new JsonObject { ["hash"] = value.ContentHash };
                if (value.Version is not null)
                {
                    entry["version"] = value.Version;
                }

                root[key] = entry;
            }

            File.WriteAllText(_storePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // Best-effort: an in-memory record still takes effect this session even if it can't persist.
        }
    }
}

/// <summary>
/// Decides whether a bundled sample script should (re)write its target file: yes for a fresh
/// install (no target, never tracked), no if the target is missing but we have a record for it
/// (the user - or another tool acting on the same folder, e.g. the Script Manager's "Remove" -
/// deleted it on purpose; don't fight that by recreating it every time this reloads), no for a
/// pre-existing file we've never tracked (don't clobber something we don't know the history of),
/// no if the on-disk copy no longer matches what we last installed (the user edited it - never
/// overwrite a user's changes), and otherwise only when the shipped version is strictly newer
/// than what's recorded.
/// </summary>
internal static class SampleScriptSync
{
    public static string ComputeHash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static bool ShouldOverwrite(bool targetExists, InstalledSampleScript? record, string? onDiskHash, string? shippedVersion)
    {
        if (!targetExists)
        {
            return record is null;
        }

        if (record is null)
        {
            return false;
        }

        if (!string.Equals(onDiskHash, record.ContentHash, StringComparison.Ordinal))
        {
            return false;
        }

        return IsNewerVersion(shippedVersion, record.Version);
    }

    public static bool IsNewerVersion(string? candidate, string? baseline)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(baseline))
        {
            return true;
        }

        if (Version.TryParse(candidate, out var c) && Version.TryParse(baseline, out var b))
        {
            return c > b;
        }

        return string.CompareOrdinal(candidate, baseline) > 0;
    }
}

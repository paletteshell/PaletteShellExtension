using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PaletteShellExtension.Classes;

/// <summary>Content hash of a bundled sample script as of the last time this process wrote it
/// to the scripts folder, so a later run can tell whether the shipped copy has moved on and
/// whether the user has since edited their copy.</summary>
internal sealed record InstalledSampleScript(string ContentHash);

/// <summary>
/// Tracks the hash of each bundled sample script this process has written into the scripts
/// folder, plus the app version that last completed a full sample sync so an unchanged
/// install can skip the sync entirely. State is a small JSON file (<c>sample-scripts.json</c>)
/// alongside <c>pinned.txt</c> and <c>community-installed.json</c> - same best-effort,
/// never-throw philosophy as those: a locked or corrupt store never breaks startup, it just
/// starts empty for the session (which just means samples fall back to "don't overwrite"
/// until it can write again).
/// </summary>
internal sealed class InstalledSampleScripts
{
    private const string StoreFileName = "sample-scripts.json";

    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };

    private readonly string _storePath;
    private readonly Dictionary<string, InstalledSampleScript> _installed;

    /// <summary>The app version whose bundled samples were last fully synced into this folder,
    /// or null when no sync has completed (fresh folder, legacy store, or corrupt store).
    /// Shipped sample content can only change with the app version, so a match means the whole
    /// sync pass can be skipped.</summary>
    public string? SyncedAppVersion { get; private set; }

    public InstalledSampleScripts(string rootDirectory)
    {
        _storePath = Path.Combine(rootDirectory, StoreFileName);
        (_installed, SyncedAppVersion) = Load(_storePath);
    }

    /// <summary>Stamps the store with the app version whose sync pass just completed cleanly
    /// and persists everything recorded so far.</summary>
    public void MarkSynced(string appVersion)
    {
        SyncedAppVersion = appVersion;
        Save();
    }

    public bool TryGet(string fileName, out InstalledSampleScript? record) =>
        _installed.TryGetValue(fileName, out record);

    /// <summary>Records (or updates) the sample's installed hash and persists the change.</summary>
    public void Record(string fileName, string contentHash)
    {
        Record(fileName, contentHash, persist: true);
    }

    internal void Record(string fileName, string contentHash, bool persist)
    {
        _installed[fileName] = new InstalledSampleScript(contentHash);
        if (persist)
        {
            Save();
        }
    }

    internal void Save() => SaveCore();

    private static (Dictionary<string, InstalledSampleScript> Installed, string? SyncedAppVersion) Load(string storePath)
    {
        var map = new Dictionary<string, InstalledSampleScript>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (File.Exists(storePath))
            {
                var root = JsonNode.Parse(File.ReadAllText(storePath))?.AsObject();
                if (root is not null)
                {
                    // Current format nests the per-script records under "scripts" with the
                    // version stamp alongside; the legacy format was the flat map itself.
                    // Sample file names always end in ".ps1", so "scripts" can't be a record key.
                    var scripts = root["scripts"] as JsonObject ?? root;
                    var version = ReferenceEquals(scripts, root) ? null : root["appVersion"]?.ToString();

                    foreach (var (key, value) in scripts)
                    {
                        var obj = value as JsonObject;
                        var hash = obj?["hash"]?.ToString();
                        if (hash is not null)
                        {
                            map[key] = new InstalledSampleScript(hash);
                        }
                    }

                    return (map, version);
                }
            }
        }
        catch (Exception)
        {
            // Missing or corrupt store - start with nothing tracked rather than failing startup.
            map.Clear();
        }

        return (map, null);
    }

    private void SaveCore()
    {
        try
        {
            var scripts = new JsonObject();
            foreach (var (key, value) in _installed.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                scripts[key] = new JsonObject { ["hash"] = value.ContentHash };
            }

            var root = new JsonObject { ["scripts"] = scripts };
            if (SyncedAppVersion is not null)
            {
                root["appVersion"] = SyncedAppVersion;
            }

            File.WriteAllText(_storePath, root.ToJsonString(SaveOptions));
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
/// overwrite a user's changes), and otherwise only when the shipped content actually differs from
/// what was last installed.
/// </summary>
internal static class SampleScriptSync
{
    public static string ComputeHash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static bool ShouldOverwrite(bool targetExists, InstalledSampleScript? record, string? onDiskHash, string shippedHash)
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

        return !string.Equals(shippedHash, record.ContentHash, StringComparison.Ordinal);
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Commands;
using PaletteShellExtension.Pages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PaletteShellExtension;


internal sealed partial class PaletteShellExtensionPage : ListPage
{
    private static readonly string SuggestedDefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "PaletteShellScripts");

    private string? _rootDirectory;
    private PinnedScripts? _pins;
    private List<FileInfo> _files = [];
    private IListItem[]? _cachedItems;
    private readonly ConcurrentDictionary<string, CachedManifestEntry> _manifestCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CachedManifestEntry(long Length, DateTime LastWriteTimeUtc, ScriptParseResult Result);

    // Set when the configured scripts folder couldn't be created or scanned (unplugged
    // USB drive, offline share, changed drive letter). GetItems() surfaces it instead of
    // the failure taking down COM activation. The persisted setting is deliberately left
    // alone — the folder may come back, and the user can repoint via setup meanwhile.
    private string? _folderError;

    public PaletteShellExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = $"PaletteShell v{AppVersion.Current}";
        Name = "PaletteShell";

        string? configuredFolder = null;
        try
        {
            configuredFolder = PaletteShellSettingsManager.Instance.ScriptsFolder;
            if (configuredFolder is null && Directory.Exists(SuggestedDefaultFolder))
            {
                // Upgrading from a version that predates this setting: the well-known default
                // folder already exists (with the user's scripts and pins in it), so adopt it
                // silently instead of prompting someone who's already set up.
                PaletteShellSettingsManager.Instance.ScriptsFolder = SuggestedDefaultFolder;
                configuredFolder = SuggestedDefaultFolder;
            }

            if (configuredFolder is not null)
            {
                InitializeFolder(configuredFolder);
            }
            // Else: genuinely first run, no folder configured and no pre-existing default folder
            // — GetItems() prompts for one instead of silently defaulting, and InitializeFolder
            // runs once the user picks one.
        }
        catch (Exception ex)
        {
            // This constructor runs during COM activation; throwing here would crash the
            // extension process on every launch until the folder problem resolved itself.
            // Degrade to the setup state instead and say which folder failed.
            Log.Error($"Failed to initialize scripts folder '{configuredFolder ?? "(none)"}'", ex);
            _rootDirectory = null;
            _pins = null;
            _folderError = configuredFolder;
        }
    }

    // Points the page at the given folder, creating it and copying in the sample scripts and
    // supporting module/docs, then scanning it. Runs both on normal startup (folder already
    // configured) and once the first-run setup form or "Reload scripts" picks up a new folder.
    private void InitializeFolder(string folder)
    {
        _rootDirectory = folder;
        Directory.CreateDirectory(folder);
        _pins = new PinnedScripts(folder);
        CopySampleScripts(folder);
        CopyPowerShellModule(folder);
        RefreshFiles();
    }

    // Called by the setup form once the user chooses a folder for the first time. The form
    // already validated it can create the folder, but the scan can still fail (e.g. access
    // denied on an existing folder) — degrade like the constructor does rather than letting
    // the exception travel back through the form's COM call.
    private void HandleFolderConfigured(string folder)
    {
        try
        {
            _folderError = null;
            InitializeFolder(folder);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to initialize scripts folder '{folder}'", ex);
            _rootDirectory = null;
            _pins = null;
            _folderError = folder;
        }

        RaiseItemsChanged();
    }

    /// <summary>Rescans the scripts folder and refreshes the list. Returns the number of
    /// .ps1 scripts found so callers (e.g. the Reload command) can confirm completion.
    /// Also picks up a folder changed via the settings page since the page was built.</summary>
    public int RefreshFiles()
    {
        _cachedItems = null; // Clear cache

        if (_rootDirectory is null)
        {
            return 0;
        }

        try
        {
            // The folder may have been repointed via the settings page since this page was
            // built (or since the last reload) — re-derive it here so "Reload scripts" is the
            // single, explicit action that picks up a relocation, consistent with how it's
            // already the single action that picks up new/edited scripts.
            var configuredFolder = PaletteShellSettingsManager.Instance.ScriptsFolder;
            if (configuredFolder is not null && !string.Equals(configuredFolder, _rootDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _rootDirectory = configuredFolder;
                _manifestCache.Clear();
                Directory.CreateDirectory(configuredFolder);
                _pins = new PinnedScripts(configuredFolder);
                CopySampleScripts(configuredFolder);
                CopyPowerShellModule(configuredFolder);
            }

            var rootDirectory = _rootDirectory;

            // Enumerate as FileInfo rather than paths: the directory listing already carries each
            // file's size and write time, so the manifest-cache check in GetItems can reuse them
            // instead of paying a second stat per script (noticeable on synced/network folders).
            var discovered = new DirectoryInfo(rootDirectory).EnumerateFiles("*.ps1", SearchOption.TopDirectoryOnly).ToList();

                        _files = [.. discovered];
            PruneManifestCache(_files.Select(f => f.FullName));
            _folderError = null;
        }
        catch (Exception ex)
        {
            // The folder went away between launches or mid-session (drive unplugged, share
            // offline). Keep the page alive with an empty scan and a visible error item
            // rather than throwing back through the host's COM call.
            Log.Error($"Failed to scan scripts folder '{_rootDirectory}'", ex);
            _files = [];
            _folderError = _rootDirectory;
        }

        // Use the page's change notification so CmdPal asks for items again.
        RaiseItemsChanged();

        return _files.Count;
    }

    private static void CopySampleScripts(string root)
    {
        var installedSamples = new InstalledSampleScripts(root);

        // Shipped sample content can only change when the app version does, so a folder
        // already synced by this version has nothing to do — skip the whole pass instead of
        // re-reading and re-hashing every sample each activation (which, with the default
        // folder under a OneDrive-backed Documents, can even hydrate placeholder files).
        var currentVersion = AppVersion.Current.ToString();
        if (string.Equals(installedSamples.SyncedAppVersion, currentVersion, StringComparison.Ordinal))
        {
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("SampleScripts") && n.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var anyFailures = false;

        foreach (var resourceName in resourceNames)
        {
            var fileName = resourceName.Split('.').Reverse().Skip(1).First() + ".ps1";
            var targetPath = Path.Combine(root, fileName);

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var content = reader.ReadToEnd();

                var targetExists = File.Exists(targetPath);
                var onDiskHash = targetExists ? SampleScriptSync.ComputeHash(File.ReadAllText(targetPath, Encoding.UTF8)) : null;
                installedSamples.TryGet(fileName, out var record);
                var shippedHash = SampleScriptSync.ComputeHash(content);

                // Only (re)write when it's a fresh install, or the bundled content changed since
                // we last installed it and the on-disk copy still matches what we installed
                // (i.e. not user-modified).
                if (SampleScriptSync.ShouldOverwrite(targetExists, record, onDiskHash, shippedHash))
                {
                    File.WriteAllText(targetPath, content, new UTF8Encoding(false));
                    installedSamples.Record(fileName, shippedHash, persist: false);
                }
            }
            catch (Exception ex)
            {
                anyFailures = true;
                Log.Warn($"Failed to copy sample script '{fileName}': {ex.Message}");
            }
        }

        // Only stamp a clean pass: a transient failure (e.g. a locked file) leaves the stamp
        // stale so the next activation retries, matching the old every-launch behavior. Any
        // samples that did copy are still persisted so their edits-vs-updates tracking holds.
        if (anyFailures)
        {
            installedSamples.Save();
        }
        else
        {
            installedSamples.MarkSynced(currentVersion);
        }
    }

    private static void CopyPowerShellModule(string root)
    {
        var baseDir = AppContext.BaseDirectory;

        // Copy the PowerShell module
        var moduleSourcePath = Path.Combine(baseDir, "PaletteScriptAttributes.psm1");
        var moduleTargetPath = Path.Combine(root, "PaletteScriptAttributes.psm1");

        if (File.Exists(moduleSourcePath))
        {
            try
            {
                CopyIfChanged(moduleSourcePath, moduleTargetPath);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to copy PaletteScriptAttributes.psm1 to scripts folder: {ex.Message}");
            }
        }

        // Copy the short agent-facing guide and full reference so tools pointed at the scripts
        // folder can start cheaply, then opt into the detailed contract when needed.
        var agentsSourcePath = Path.Combine(baseDir, "AGENTS.md");
        var agentsTargetPath = Path.Combine(root, "AGENTS.md");
        var referenceSourcePath = Path.Combine(baseDir, "PaletteShellScripts.Reference.md");
        var referenceTargetPath = Path.Combine(root, "PaletteShellScripts.Reference.md");

        if (File.Exists(agentsSourcePath))
        {
            try
            {
                CopyIfChanged(agentsSourcePath, agentsTargetPath);
            }
            catch (Exception)
            {
                // Authoring spec copy is best-effort.
            }
        }

        if (File.Exists(referenceSourcePath))
        {
            try
            {
                CopyIfChanged(referenceSourcePath, referenceTargetPath);
            }
            catch (Exception)
            {
                // Authoring reference copy is best-effort.
            }
        }
    }

    // Skips the copy when the target already matches the source (same size and write time),
    // so a launch that changes nothing doesn't pay for redundant disk writes.
    private static void CopyIfChanged(string source, string target)
    {
        if (File.Exists(target))
        {
            var sourceInfo = new FileInfo(source);
            var targetInfo = new FileInfo(target);
            if (sourceInfo.Length == targetInfo.Length && sourceInfo.LastWriteTimeUtc == targetInfo.LastWriteTimeUtc)
            {
                return;
            }
        }

        File.Copy(source, target, overwrite: true);
    }

    public override IListItem[] GetItems()
    {
        if (_rootDirectory is not { } rootDirectory || _pins is not { } pins)
        {
            // First run (no folder configured yet) or the configured folder couldn't be
            // accessed at startup. Prompt for one instead of silently defaulting; everything
            // else (samples, module, script scan) waits for it.
            return
            [
                new ListItem(new ScriptsFolderSetupPage(SuggestedDefaultFolder, HandleFolderConfigured))
                {
                    Title = "Choose scripts folder",
                    Subtitle = _folderError is null
                        ? "Pick where PaletteShell should look for your .ps1 scripts"
                        : $"⚠ Couldn't access '{_folderError}' — reconnect it or pick another folder",
                },
            ];
        }

        if (_cachedItems != null)
        {
            return _cachedItems;
        }

        List<IListItem> items = [];

        // A configured folder that failed its last scan gets a visible banner (with a retry
        // via the reload command) instead of an unexplained empty list.
        if (_folderError is not null)
        {
            items.Add(new ListItem(new ReloadPageCommand(this))
            {
                Title = "⚠ Couldn't read the scripts folder",
                Subtitle = $"'{_folderError}' — reconnect it, then press Enter to retry",
            });
        }

        items.AddRange([
            new ListItem(new OpenFolderCommand(rootDirectory)) { Title = "Open scripts folder" },
            new ListItem(new OpenFolderCommand(Log.LogDirectory, "Open log folder"))
            {
                Title = "Open log folder",
                Subtitle = "Diagnostic logs for script runs and failures",
            },
            new ListItem(new ReloadPageCommand(this)) { Title = "Reload scripts" },
            new ListItem(new NewScriptWizardPage(rootDirectory)) { Title = "Create new script", Subtitle = "Add a scaffolded .ps1 with metadata headers" },
            new ListItem(new LaunchCommunityStoreCommand())
            {
                Title = "Browse community scripts",
                Subtitle = "Open the Script Manager, or browse the community repo if it isn't installed",
                MoreCommands = [
                    new CommandContextItem(new OpenLinkCommand("View repository on GitHub", "https://github.com/paletteshell/PaletteShellScripts", "")),
                ],
            },
        ]);

        // Script items are sorted below: pinned scripts first, then alphabetically by their
        // displayed Title (not by filename, since the manifest Title often differs from it).
        // The Pinned flag is captured per item so the sort doesn't have to re-read the store.
        //
        // Parsing each script is independent (PowerShellScriptParser is stateless, and _pins
        // is only read here, never mutated concurrently), so this runs in parallel and writes
        // into a slot per index rather than a shared List<T>.Add. Sorting below is unaffected
        // by completion order, so results are identical to the sequential version.
        var scriptResults = new (bool Pinned, string Title, IListItem Item)?[_files.Count];

        // Probe for pwsh 7 and read the default host once, not per script: both feed the
        // compatibility gate below, which stays a pure check so it can run in the parallel loop.
        var pwshAvailable = ScriptRunner.IsPwshInstalled();
        var defaultHost = PaletteShellSettingsManager.Instance.DefaultHost;

        Parallel.For(0, _files.Count, i =>
        {
            var file = _files[i];
            var path = file.FullName;
            try
            {
                var parseResult = GetCachedManifest(file);

                // Metadata was malformed enough that the parser failed closed. Show a disabled
                // repair row that opens the script for editing instead of running a partially
                // understood script (which could launch fire-and-forget and report false success).
                if (parseResult.HasErrors)
                {
                    var repairPinned = pins.IsPinned(path);
                    var repairTitle = Path.GetFileNameWithoutExtension(path);
                    // The subtitle truncates the parse error; Enter shows the full reason in a
                    // dialog whose primary action opens the script to fix it.
                    scriptResults[i] = (repairPinned, repairTitle, new ListItem(new CallbackCommand("View error", () =>
                        WarningDialog.Show(
                            $"Couldn't load {Path.GetFileName(path)}",
                            parseResult.FirstError ?? "The script's metadata couldn't be parsed.",
                            "Open to fix",
                            () =>
                            {
                                EditorLauncher.Open(path);
                                return CommandResult.Dismiss();
                            }))
                    )
                    {
                        Title = repairTitle,
                        Subtitle = $"⚠ Couldn't load this script — {parseResult.FirstError} Open to fix ({Path.GetFileName(path)})",
                        Icon = new IconInfo(""), // Warning
                        MoreCommands = BuildContextCommands(path, pins)
                    });
                    return;
                }

                var manifest = parseResult.Manifest;
                var title = manifest?.Title ?? Path.GetFileNameWithoutExtension(path);
                var subtitle = manifest?.Description ?? path;

                // One gate covers app-version range and the elevation/output-capture conflict for
                // every route below: a blocked script gets a warning row instead of a runnable one.
                var compat = ScriptCompatibility.Validate(manifest, pwshAvailable, defaultHost);
                if (compat.Kind == ScriptCompatibilityKind.RequiresUpdate)
                {
                    var incompatiblePinned = pins.IsPinned(path);
                    var incompatibleSubtitle = compat.TooNew
                        ? $"⚠ Requires PaletteShell v{compat.RequiredVersion} or earlier — you have v{AppVersion.Current}"
                        : $"⚠ Requires PaletteShell v{compat.RequiredVersion} or later — you have v{AppVersion.Current}";
                    scriptResults[i] = (incompatiblePinned, title, new ListItem(new IncompatibleScriptCommand(compat.RequiredVersion!, AppVersion.Current.ToString(), compat.TooNew))
                    {
                        Title = title,
                        Subtitle = incompatibleSubtitle,
                        Icon = new IconInfo(""), // Warning
                        MoreCommands = BuildContextCommands(path, pins)
                    });
                    return;
                }

                if (compat.Kind == ScriptCompatibilityKind.UnknownHost)
                {
                    var unknownHostPinned = pins.IsPinned(path);
                    scriptResults[i] = (unknownHostPinned, title, new ListItem(new UnknownHostCommand(compat.BadHost!))
                    {
                        Title = title,
                        Subtitle = $"⚠ Unknown script host '{compat.BadHost}' — use auto, pwsh, or powershell",
                        Icon = new IconInfo(""), // Warning
                        MoreCommands = BuildContextCommands(path, pins)
                    });
                    return;
                }

                if (compat.Kind == ScriptCompatibilityKind.PwshMissing)
                {
                    var pwshMissingPinned = pins.IsPinned(path);
                    scriptResults[i] = (pwshMissingPinned, title, new ListItem(new PwshMissingCommand())
                    {
                        Title = title,
                        Subtitle = "⚠ Requires PowerShell 7 (pwsh) — not installed. Install from https://aka.ms/powershell, or set host to 'auto'",
                        Icon = new IconInfo(""), // Warning
                        MoreCommands = BuildContextCommands(path, pins)
                    });
                    return;
                }

                if (compat.Kind == ScriptCompatibilityKind.ElevationIncompatible)
                {
                    var elevationPinned = pins.IsPinned(path);
                    scriptResults[i] = (elevationPinned, title, new ListItem(new ElevationIncompatibleCommand())
                    {
                        Title = title,
                        Subtitle = ScriptElevation.IncompatibleReason(),
                        Icon = new IconInfo(""), // Warning
                        MoreCommands = BuildContextCommands(path, pins)
                    });
                    return;
                }

                var wantsMarkdown = string.Equals(manifest?.Output, "Markdown", StringComparison.OrdinalIgnoreCase);
                var wantsList = string.Equals(manifest?.Output, "List", StringComparison.OrdinalIgnoreCase);
                var wantsResult = string.Equals(manifest?.Output, "Result", StringComparison.OrdinalIgnoreCase);

                // Resolve host/cwd/env/timeout/elevation once, here, so every route below shares
                // the same execution decisions instead of each re-deriving them.
                var plan = manifest is not null
                    ? ScriptExecutionService.CreatePlan(manifest, path)
                    : null;

                ICommand command;
                if (wantsList && manifest is not null)
                {
                    // List output - navigate to a page that runs the script and turns its
                    // stdout into a searchable, pickable list. If the script declares a
                    // parameter, that page feeds it the palette's search text (it acts as a
                    // live provider) rather than using the parameter form.
                    command = new ScriptListPage(path, manifest, plan!);
                }
                else if (manifest?.Parameters is { Count: > 0 })
                {
                    // Script has parameters - navigate to parameter form page
                    command = new ScriptParameterFormPage(path, manifest, plan!);
                }
                else if (wantsMarkdown && manifest is not null)
                {
                    // No parameters, Markdown output - navigate to a page that runs
                    // the script and renders its stdout as Markdown.
                    command = new ScriptMarkdownPage(path, manifest, plan!);
                }
                else if (wantsResult && manifest is not null)
                {
                    // No parameters, Result output - navigate to a page that runs the script
                    // and shows its output as a single copyable result (Enter copies), the way
                    // a calculator shows an answer.
                    command = new ScriptResultPage(path, manifest, plan!);
                }
                else if (plan is not null && (plan.DeclaredTimeoutMs is not null || plan.SurfacesOutput))
                {
                    // No parameters, but a waited ambient mode (Toast/Clipboard/Open/File, or None
                    // with a declared timeout): dismiss the palette and run off-thread, performing
                    // the clipboard/open/file side effect and toasting completion via a host banner —
                    // so a fire-and-forget script neither freezes the host nor parks a page the user
                    // has to dismiss. Display modes (Result/Markdown/List) branched off above.
                    command = new AmbientRunCommand(path, manifest!, plan);
                }
                else
                {
                    // No parameters, nothing to wait for or surface (None output with no declared
                    // timeout, or no manifest at all) - launch fire-and-forget.
                    command = new RunScriptCommand(path, manifest);
                }

                var pinned = pins.IsPinned(path);

                var listItem = new ListItem(command)
                {
                    Title = title,
                    Subtitle = subtitle,
                    Icon = !string.IsNullOrWhiteSpace(manifest?.IconGlyph)
                        ? new IconInfo(manifest.IconGlyph)
                        : DefaultScriptIcon,
                    MoreCommands = BuildContextCommands(path, pins)
                };

                scriptResults[i] = (pinned, title, listItem);
            }
            catch (Exception ex)
            {
                // Building the rich entry failed (e.g. a malformed parameter block). Rather than
                // dropping the script silently, surface it with an error hint so the user can
                // find it, open it to fix, or remove it — instead of wondering where it went.
                Log.Warn($"Failed to build list item for '{path}': {ex.Message}");
                try
                {
                    var pinned = pins.IsPinned(path);
                    var errorTitle = Path.GetFileNameWithoutExtension(path);
                    scriptResults[i] = (pinned, errorTitle, new ListItem(new OpenInEditorCommand(path))
                    {
                        Title = errorTitle,
                        Subtitle = $"⚠ Couldn't load this script — open to inspect ({Path.GetFileName(path)})",
                        Icon = new IconInfo(""), // Warning
                        MoreCommands = BuildContextCommands(path, pins)
                    });
                }
                catch (Exception inner)
                {
                    // Even the rich error entry failed (e.g. the pin store or icon machinery is
                    // what threw in the first place). Fill the slot with the barest possible item
                    // so it is never left null — the sort below dereferences every slot.
                    Log.Warn($"Failed to build fallback item for '{path}': {inner.Message}");
                    var bareTitle = Path.GetFileNameWithoutExtension(path);
                    scriptResults[i] = (false, bareTitle, new ListItem(new OpenInEditorCommand(path))
                    {
                        Title = bareTitle,
                    });
                }
            }
        });

        // Keep the system commands at the very top; then pinned scripts, then the rest —
        // each group alphabetical by title. Every slot is filled by the loop above (even its
        // catch has a fallback), but skip any null defensively — a missing row beats throwing
        // the whole list away through the COM boundary.
        items.AddRange(scriptResults
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .OrderByDescending(i => i.Pinned)
            .ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(i => i.Item));

        _cachedItems = [.. items];
        return _cachedItems;
    }

    // Fallback glyph for scripts that don't declare their own [ScriptIcon]. Keeps the list
    // scannable instead of showing rows with no icon at all.
    private static readonly IconInfo DefaultScriptIcon = new(""); // CommandPrompt

    // Takes the FileInfo from RefreshFiles' enumeration so the size/write-time cache check
    // doesn't re-stat the file. The snapshot being from scan time is fine: picking up
    // between-reload edits was never promised — "Reload scripts" is the refresh point.
    private ScriptParseResult GetCachedManifest(FileInfo info)
    {
        var path = info.FullName;
        try
        {
            if (_manifestCache.TryGetValue(path, out var cached)
                && cached.Length == info.Length
                && cached.LastWriteTimeUtc == info.LastWriteTimeUtc)
            {
                return cached.Result;
            }

            var result = PowerShellScriptParser.TryParse(path);
            _manifestCache[path] = new CachedManifestEntry(info.Length, info.LastWriteTimeUtc, result);
            return result;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to stat manifest for '{path}': {ex.Message}");
            _manifestCache.TryRemove(path, out _);
            return PowerShellScriptParser.TryParse(path);
        }
    }

    private void PruneManifestCache(IEnumerable<string> currentFiles)
    {
        var current = new HashSet<string>(currentFiles, StringComparer.OrdinalIgnoreCase);
        foreach (var cachedPath in _manifestCache.Keys)
        {
            if (!current.Contains(cachedPath))
            {
                _manifestCache.TryRemove(cachedPath, out _);
            }
        }
    }

    // Per-script context menu shared by normal and error entries: pin, open, reveal, delete.
    private CommandContextItem[] BuildContextCommands(string path, PinnedScripts pins) =>
    [
        new CommandContextItem(new TogglePinCommand(path, pins, () => RefreshFiles())),
        new CommandContextItem(new OpenInEditorCommand(path)),
        new CommandContextItem(new RevealInExplorerCommand(path)),
        new CommandContextItem(new DeleteScriptCommand(path, () => RefreshFiles())),
    ];

}

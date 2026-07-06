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
    private List<string> _files = [];
    private IListItem[]? _cachedItems;
    private readonly ConcurrentDictionary<string, CachedManifestEntry> _manifestCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CachedManifestEntry(long Length, DateTime LastWriteTimeUtc, ScriptManifest? Manifest);

    public PaletteShellExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "PaletteShell";
        Name = "PaletteShell";

        var configuredFolder = PaletteShellSettingsManager.Instance.ScriptsFolder;
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

    // Points the page at the given folder, creating it and copying in the sample scripts and
    // supporting module/docs, then scanning it. Runs both on normal startup (folder already
    // configured) and once the first-run setup form or "Reload scripts" picks up a new folder.
    private void InitializeFolder(string folder)
    {
        _rootDirectory = folder;
        Directory.CreateDirectory(folder);
        _pins = new PinnedScripts(folder);
        CopySampleScripts(folder);
        RefreshFiles();

        // The module/docs/dll files copied here are never *.ps1 files, so RefreshFiles()
        // never picks them up and they can't affect what GetItems() shows — safe to push
        // off the constructor's critical path instead of blocking the first render on them.
        _ = Task.Run(() => CopyPowerShellModule(folder));
    }

    // Called by the setup form once the user chooses a folder for the first time.
    private void HandleFolderConfigured(string folder)
    {
        InitializeFolder(folder);
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
            _ = Task.Run(() => CopyPowerShellModule(configuredFolder));
        }

        var rootDirectory = _rootDirectory;

        var files = Directory.GetFiles(rootDirectory, "*.ps1", SearchOption.TopDirectoryOnly);
        _files = [.. files];
        PruneManifestCache(_files);

        // Use the page's change notification so CmdPal asks for items again.
        RaiseItemsChanged();

        return _files.Count;
    }

    private static void CopySampleScripts(string root)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("SampleScripts") && n.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var installedSamples = new InstalledSampleScripts(root);
        var recordedAnySamples = false;

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
                var shippedVersion = PowerShellScriptParser.ParseManifestFromContent(content).Version;

                // Only (re)write when it's a fresh install, or a newer shipped version whose
                // on-disk copy still matches what we last installed (i.e. not user-modified).
                if (SampleScriptSync.ShouldOverwrite(targetExists, record, onDiskHash, shippedVersion))
                {
                    File.WriteAllText(targetPath, content, new UTF8Encoding(false));
                    installedSamples.Record(fileName, shippedVersion, SampleScriptSync.ComputeHash(content), persist: false);
                    recordedAnySamples = true;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to copy sample script '{fileName}': {ex.Message}");
            }
        }

        if (recordedAnySamples)
        {
            installedSamples.Save();
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

        // Copy the agent-facing authoring spec so tools pointed at the scripts folder discover
        // the script contract. Best-effort and always refreshed to stay in sync with the extension.
        var agentsSourcePath = Path.Combine(baseDir, "AGENTS.md");
        var agentsTargetPath = Path.Combine(root, "AGENTS.md");

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

        // Copy TextCopy.dll and its dependencies so PowerShell can load it
        var textCopySource = Path.Combine(baseDir, "TextCopy.dll");
        var textCopyTarget = Path.Combine(root, "TextCopy.dll");

        if (File.Exists(textCopySource))
        {
            try
            {
                CopyIfChanged(textCopySource, textCopyTarget);
            }
            catch (Exception)
            {
                // Scripts will fall back to Windows Forms clipboard.
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
            // First run - no folder configured yet. Prompt for one instead of silently
            // defaulting; everything else (samples, module, script scan) waits for it.
            return
            [
                new ListItem(new ScriptsFolderSetupPage(SuggestedDefaultFolder, HandleFolderConfigured))
                {
                    Title = "Choose scripts folder",
                    Subtitle = "Pick where PaletteShell should look for your .ps1 scripts",
                },
            ];
        }

        if (_cachedItems != null)
        {
            return _cachedItems;
        }

        List<IListItem> items = [
            new ListItem(new OpenFolderCommand(rootDirectory)) { Title = "Open scripts folder" },
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
        ];

        // Script items are sorted below: pinned scripts first, then alphabetically by their
        // displayed Title (not by filename, since the manifest Title often differs from it).
        // The Pinned flag is captured per item so the sort doesn't have to re-read the store.
        //
        // Parsing each script is independent (PowerShellScriptParser is stateless, and _pins
        // is only read here, never mutated concurrently), so this runs in parallel and writes
        // into a slot per index rather than a shared List<T>.Add. Sorting below is unaffected
        // by completion order, so results are identical to the sequential version.
        var scriptResults = new (bool Pinned, string Title, IListItem Item)?[_files.Count];

        Parallel.For(0, _files.Count, i =>
        {
            var path = _files[i];
            try
            {
                var manifest = GetCachedManifest(path);
                var title = manifest?.Title ?? Path.GetFileNameWithoutExtension(path);
                var subtitle = manifest?.Description ?? path;

                if (manifest is not null && !AppVersion.IsCompatible(manifest.MinVersion, manifest.MaxVersion, out var requiredVersion, out var tooNew))
                {
                    var incompatiblePinned = pins.IsPinned(path);
                    var incompatibleSubtitle = tooNew
                        ? $"⚠ Requires PaletteShell v{requiredVersion} or earlier — you have v{AppVersion.Current}"
                        : $"⚠ Requires PaletteShell v{requiredVersion} or later — you have v{AppVersion.Current}";
                    scriptResults[i] = (incompatiblePinned, title, new ListItem(new IncompatibleScriptCommand(requiredVersion!.ToString(), AppVersion.Current.ToString(), tooNew))
                    {
                        Title = title,
                        Subtitle = incompatibleSubtitle,
                        Icon = new IconInfo(""), // Warning
                        MoreCommands = BuildContextCommands(path, pins)
                    });
                    return;
                }

                var wantsMarkdown = string.Equals(manifest?.Output, "Markdown", StringComparison.OrdinalIgnoreCase);
                var wantsList = string.Equals(manifest?.Output, "List", StringComparison.OrdinalIgnoreCase);
                var wantsResult = string.Equals(manifest?.Output, "Result", StringComparison.OrdinalIgnoreCase);

                ICommand command;
                if (wantsList && manifest is not null)
                {
                    // List output - navigate to a page that runs the script and turns its
                    // stdout into a searchable, pickable list. If the script declares a
                    // parameter, that page feeds it the palette's search text (it acts as a
                    // live provider) rather than using the parameter form.
                    var resolvedCwd = PowerShellScriptParser.ResolveCwd(manifest.Cwd, path);

                    command = new ScriptListPage(
                        scriptPath: path,
                        manifest: manifest,
                        host: manifest.Host ?? PaletteShellSettingsManager.Instance.DefaultHost,
                        cwd: resolvedCwd,
                        env: manifest.Env);
                }
                else if (manifest?.Parameters is { Count: > 0 })
                {
                    // Script has parameters - navigate to parameter form page
                    var resolvedCwd = PowerShellScriptParser.ResolveCwd(manifest.Cwd, path);

                    var formPage = new ScriptParameterFormPage(
                        scriptPath: path,
                        manifest: manifest,
                        host: manifest.Host ?? PaletteShellSettingsManager.Instance.DefaultHost,
                        cwd: resolvedCwd,
                        env: manifest.Env
                    );

                    command = formPage;
                }
                else if (wantsMarkdown && manifest is not null)
                {
                    // No parameters, Markdown output - navigate to a page that runs
                    // the script and renders its stdout as Markdown.
                    var resolvedCwd = PowerShellScriptParser.ResolveCwd(manifest.Cwd, path);

                    command = new ScriptMarkdownPage(
                        scriptPath: path,
                        manifest: manifest,
                        host: manifest.Host ?? PaletteShellSettingsManager.Instance.DefaultHost,
                        cwd: resolvedCwd,
                        env: manifest.Env);
                }
                else if (wantsResult && manifest is not null)
                {
                    // No parameters, Result output - navigate to a page that runs the script
                    // and shows its output as a single copyable result (Enter copies), the way
                    // a calculator shows an answer.
                    var resolvedCwd = PowerShellScriptParser.ResolveCwd(manifest.Cwd, path);

                    command = new ScriptResultPage(
                        scriptPath: path,
                        manifest: manifest,
                        host: manifest.Host ?? PaletteShellSettingsManager.Instance.DefaultHost,
                        cwd: resolvedCwd,
                        env: manifest.Env);
                }
                else
                {
                    // No parameters - run script directly
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
        });

        // Keep the system commands at the very top; then pinned scripts, then the rest —
        // each group alphabetical by title.
        items.AddRange(scriptResults
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

    private ScriptManifest? GetCachedManifest(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (_manifestCache.TryGetValue(path, out var cached)
                && cached.Length == info.Length
                && cached.LastWriteTimeUtc == info.LastWriteTimeUtc)
            {
                return cached.Manifest;
            }

            var manifest = PowerShellScriptParser.TryParseManifest(path);
            _manifestCache[path] = new CachedManifestEntry(info.Length, info.LastWriteTimeUtc, manifest);
            return manifest;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to stat manifest for '{path}': {ex.Message}");
            _manifestCache.TryRemove(path, out _);
            return PowerShellScriptParser.TryParseManifest(path);
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

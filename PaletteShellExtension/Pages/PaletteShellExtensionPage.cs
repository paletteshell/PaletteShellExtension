// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Commands;
using PaletteShellExtension.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PaletteShellExtension;


internal sealed partial class PaletteShellExtensionPage : ListPage
{
    private readonly string _rootDirectory;
    private readonly PinnedScripts _pins;
    private List<string> _files = [];
    private IListItem[]? _cachedItems;

    public PaletteShellExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "PaletteShell";
        Name = "PaletteShell";

        _rootDirectory = Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
           "PaletteShellScripts");

        Directory.CreateDirectory(_rootDirectory);
        _pins = new PinnedScripts(_rootDirectory);
        CopySampleScripts();
        RefreshFiles();

        // The module/docs/dll files copied here are never *.ps1 files, so RefreshFiles()
        // never picks them up and they can't affect what GetItems() shows — safe to push
        // off the constructor's critical path instead of blocking the first render on them.
        _ = Task.Run(CopyPowerShellModule);
    }

    /// <summary>Rescans the scripts folder and refreshes the list. Returns the number of
    /// .ps1 scripts found so callers (e.g. the Reload command) can confirm completion.</summary>
    public int RefreshFiles()
    {
        _cachedItems = null; // Clear cache

        var files = Directory.GetFiles(_rootDirectory, "*.ps1", SearchOption.TopDirectoryOnly);
        _files = [.. files];

        // Use the page's change notification so CmdPal asks for items again.
        RaiseItemsChanged();

        return _files.Count;
    }

    private void CopySampleScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("SampleScripts") && n.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var resourceName in resourceNames)
        {
            var fileName = resourceName.Split('.').Reverse().Skip(1).First() + ".ps1";
            var targetPath = Path.Combine(_rootDirectory, fileName);

            // Only copy if the file doesn't exist (don't overwrite user modifications)
            if (!File.Exists(targetPath))
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        var content = reader.ReadToEnd();
                        File.WriteAllText(targetPath, content, new UTF8Encoding(false));
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to copy sample script '{fileName}': {ex.Message}");
                }
            }
        }
    }

    private void CopyPowerShellModule()
    {
        var baseDir = AppContext.BaseDirectory;

        // Copy the PowerShell module
        var moduleSourcePath = Path.Combine(baseDir, "PaletteScriptAttributes.psm1");
        var moduleTargetPath = Path.Combine(_rootDirectory, "PaletteScriptAttributes.psm1");

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
        var agentsTargetPath = Path.Combine(_rootDirectory, "AGENTS.md");

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
        var textCopyTarget = Path.Combine(_rootDirectory, "TextCopy.dll");

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
        if (_cachedItems != null)
        {
            return _cachedItems;
        }

        List<IListItem> items = [
            new ListItem(new OpenFolderCommand(_rootDirectory)) { Title = "Open scripts folder" },
            new ListItem(new ReloadPageCommand(this)) { Title = "Reload scripts" },
            new ListItem(new NewScriptWizardPage(_rootDirectory)) { Title = "Create new script", Subtitle = "Add a scaffolded .ps1 with metadata headers" },
            new ListItem(new OpenLinkCommand("Find more scripts", "https://github.com/paletteshell/PaletteShellScripts", "")) { Title = "Find more scripts", Subtitle = "Browse the PaletteShellScripts repository on GitHub" },
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
                var manifest = PowerShellScriptParser.TryParseManifest(path);
                var title = manifest?.Title ?? Path.GetFileNameWithoutExtension(path);
                var subtitle = manifest?.Description ?? path;

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
                        host: manifest.Host ?? "pwsh",
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
                        host: manifest.Host ?? "pwsh",
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
                        host: manifest.Host ?? "pwsh",
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
                        host: manifest.Host ?? "pwsh",
                        cwd: resolvedCwd,
                        env: manifest.Env);
                }
                else
                {
                    // No parameters - run script directly
                    command = new RunScriptCommand(path, manifest);
                }

                var group = manifest?.Group;

                var pinned = _pins.IsPinned(path);

                var listItem = new ListItem(command)
                {
                    Title = title,
                    Subtitle = subtitle,
                    Icon = !string.IsNullOrWhiteSpace(manifest?.IconGlyph)
                        ? new IconInfo(manifest.IconGlyph)
                        : DefaultScriptIcon,
                    Tags = BuildTags(pinned, group),
                    MoreCommands = BuildContextCommands(path)
                };

                scriptResults[i] = (pinned, title, listItem);
            }
            catch (Exception ex)
            {
                // Building the rich entry failed (e.g. a malformed parameter block). Rather than
                // dropping the script silently, surface it with an error hint so the user can
                // find it, open it to fix, or remove it — instead of wondering where it went.
                Log.Warn($"Failed to build list item for '{path}': {ex.Message}");
                var pinned = _pins.IsPinned(path);
                var errorTitle = Path.GetFileNameWithoutExtension(path);
                scriptResults[i] = (pinned, errorTitle, new ListItem(new OpenInEditorCommand(path))
                {
                    Title = errorTitle,
                    Subtitle = $"⚠ Couldn't load this script — open to inspect ({Path.GetFileName(path)})",
                    Icon = new IconInfo(""), // Warning
                    Tags = BuildTags(pinned, null),
                    MoreCommands = BuildContextCommands(path)
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

    // Per-script context menu shared by normal and error entries: pin, open, reveal, delete.
    private CommandContextItem[] BuildContextCommands(string path) =>
    [
        new CommandContextItem(new TogglePinCommand(path, _pins, () => RefreshFiles())),
        new CommandContextItem(new OpenInEditorCommand(path)),
        new CommandContextItem(new RevealInExplorerCommand(path)),
        new CommandContextItem(new DeleteScriptCommand(path, () => RefreshFiles())),
    ];

    // Row tags: a "Pinned" marker (when pinned) followed by the script's group, if any. Either
    // may be absent, so an unpinned, ungrouped script gets an empty tag list.
    private static ITag[] BuildTags(bool pinned, string? group)
    {
        List<ITag> tags = [];
        if (pinned)
        {
            tags.Add(new Tag("📌 Pinned"));
        }

        if (!string.IsNullOrWhiteSpace(group))
        {
            tags.Add(new Tag(group));
        }

        return [.. tags];
    }


}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Commands;
using PaletteShellExtension.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PaletteShellExtension;


internal sealed partial class PaletteShellExtensionPage : ListPage
{
    private readonly string _rootDirectory;
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
        CopySampleScripts();
        CopyPowerShellModule();
        RefreshFiles();

    }

    public void RefreshFiles()
    {
        _files.Clear();
        _cachedItems = null; // Clear cache

        var files = Directory.GetFiles(_rootDirectory, "*.ps1", SearchOption.TopDirectoryOnly);
        _files = [.. files];

        // Use the page's change notification so CmdPal asks for items again.
        RaiseItemsChanged();
    }

    private void CopySampleScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("SampleScripts") && n.EndsWith(".ps1"))
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
                catch (Exception)
                {
                    // Skip scripts that fail to copy.
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
                File.Copy(moduleSourcePath, moduleTargetPath, overwrite: true);
            }
            catch (Exception)
            {
                // Module copy is best-effort.
            }
        }

        // Copy TextCopy.dll and its dependencies so PowerShell can load it
        var textCopySource = Path.Combine(baseDir, "TextCopy.dll");
        var textCopyTarget = Path.Combine(_rootDirectory, "TextCopy.dll");

        if (File.Exists(textCopySource))
        {
            try
            {
                File.Copy(textCopySource, textCopyTarget, overwrite: true);
            }
            catch (Exception)
            {
                // Scripts will fall back to Windows Forms clipboard.
            }
        }
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
        ];

        foreach (var path in _files.OrderBy(Path.GetFileName))
        {
            try
            {
                var manifest = PowerShellScriptParser.TryParseManifest(path);
                var title = manifest?.Title ?? Path.GetFileNameWithoutExtension(path);
                var subtitle = manifest?.Description ?? path;

                var wantsMarkdown = string.Equals(manifest?.Output, "Markdown", StringComparison.OrdinalIgnoreCase);

                ICommand command;
                if (manifest?.Parameters?.Any() == true)
                {
                    // Script has parameters - navigate to parameter form page
                    var resolvedCwd = PowerShellScriptParser.ExpandPathTokens(manifest.Cwd, path);

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
                    var resolvedCwd = PowerShellScriptParser.ExpandPathTokens(manifest.Cwd, path);

                    command = new ScriptMarkdownPage(
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

                var listItem = new ListItem(command)
                {
                    Title = title,
                    Subtitle = subtitle,
                    Icon = !string.IsNullOrWhiteSpace(manifest?.IconGlyph)
                        ? new IconInfo(manifest.IconGlyph)
                        : null
                };

                items.Add(listItem);
            }
            catch (Exception)
            {
                // Skip scripts that fail to parse or build.
            }
        }

        _cachedItems = [.. items];
        return _cachedItems;
    }


}

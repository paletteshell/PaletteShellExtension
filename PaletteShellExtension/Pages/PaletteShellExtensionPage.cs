// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Commands;
using PaletteShellExtension.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PaletteShellExtension;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class FilePathDebug
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        IntPtr hFile,
        System.Text.StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    public static string GetFinalPath(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var handle = fs.SafeFileHandle;

        var sb = new System.Text.StringBuilder(1024);
        uint result = GetFinalPathNameByHandle(handle.DangerousGetHandle(), sb, (uint)sb.Capacity, 0);

        if (result == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return sb.ToString();
    }
}
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
        Debug.WriteLine("[PAGE] ===== RefreshFiles START =====");
        Debug.WriteLine($"[PAGE] Root directory: {_rootDirectory}");

        _files.Clear();
        _cachedItems = null; // Clear cache
        Debug.WriteLine("[PAGE] Files list and cache cleared");

        var files = Directory.GetFiles(_rootDirectory, "*.ps1", SearchOption.TopDirectoryOnly);
        Debug.WriteLine($"[PAGE] Found {files.Length} .ps1 files");

        _files = [.. files];
        Debug.WriteLine($"[PAGE] Files added to internal list: {_files.Count}");

        // Use the page's change notification so CmdPal asks for items again.
        Debug.WriteLine("[PAGE] Calling RaiseItemsChanged()...");
        RaiseItemsChanged();
        Debug.WriteLine("[PAGE] RaiseItemsChanged() completed");
        Debug.WriteLine("[PAGE] ===== RefreshFiles END =====");
    }

    private void CopySampleScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("SampleScripts") && n.EndsWith(".ps1"));

        foreach (var resourceName in resourceNames)
        {
            var fileName = resourceName.Split('.').Reverse().Skip(1).First() + ".ps1";
            var targetPath = Path.Combine(_rootDirectory, fileName);

            // Only copy if the file doesn't exist (don't overwrite user modifications)
            if (!File.Exists(targetPath))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var content = reader.ReadToEnd();
                    File.WriteAllText(targetPath, content, new UTF8Encoding(false));
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
            File.Copy(moduleSourcePath, moduleTargetPath, overwrite: true);
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
            catch { /* If copy fails, scripts will fall back to Windows Forms clipboard */ }
        }
    }

    public override IListItem[] GetItems()
    {
        if (_cachedItems != null)
        {
            Debug.WriteLine("[PAGE] Returning cached items");
            return _cachedItems;
        }

        Debug.WriteLine("[PAGE] ===== GetItems START (Building cache) =====");
        Debug.WriteLine($"[PAGE] Files in list: {_files.Count}");

        List<IListItem> items = [
            new ListItem(new OpenFolderCommand(_rootDirectory)) { Title = "Open scripts folder" },
            new ListItem(new ReloadPageCommand(this)) { Title = "Reload scripts" },
            new ListItem(new NewScriptWizardPage(_rootDirectory)) { Title = "Create new script…", Subtitle = "Add a scaffolded .ps1 with metadata headers" },
        ];

        Debug.WriteLine($"[PAGE] Added {items.Count} static items");

        int scriptIndex = 0;
        foreach (var path in _files.OrderBy(Path.GetFileName))
        {
            scriptIndex++;
            Debug.WriteLine($"[PAGE] Processing script {scriptIndex}: {Path.GetFileName(path)}");

            try
            {
                var manifest = PowerShellScriptParser.TryParseManifest(path);
                var title = manifest?.Title ?? Path.GetFileNameWithoutExtension(path);
                var subtitle = manifest?.Description ?? path;

                Debug.WriteLine($"[PAGE]   Title: {title}");
                Debug.WriteLine($"[PAGE]   Subtitle: {subtitle}");

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
                else
                {
                    // No parameters - run script directly
                    command = new RunScriptCommand(path, manifest);
                }

                var listItem = new ListItem(command)
                {
                    Title = title,
                    Subtitle = subtitle
                };

                items.Add(listItem);
                Debug.WriteLine($"[PAGE]   Added to items list");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE]   ERROR processing script: {ex.Message}");
            }
        }

        Debug.WriteLine($"[PAGE] Total items to return: {items.Count}");
        Debug.WriteLine("[PAGE] ===== GetItems END (Cached) =====");

        _cachedItems = [.. items];
        return _cachedItems;
    }

  
}


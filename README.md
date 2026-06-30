# PaletteShell Extension

**PaletteShell** is a [Windows Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extension that lets you run custom PowerShell scripts directly from the Command Palette. Transform clipboard text, generate GUIDs, format JSON, and automate your daily workflows — all without leaving your keyboard.

> 💡 Looking for ready-made scripts? Browse the community script library at **[paletteshell/PaletteShellScripts](https://github.com/paletteshell/PaletteShellScripts)** — also reachable from inside the palette via the **"Find more scripts"** command.

## 🌟 Features

- **🚀 Quick Access**: Run PowerShell scripts directly from the Windows Command Palette
- **📋 Clipboard Utilities**: Transform and manipulate clipboard text with one keystroke
- **🔧 Customizable**: Drop your own `.ps1` files into a folder and they show up automatically
- **📝 Parameter Support**: Scripts with parameters get an interactive input form, generated from the script's own `param()` block
- **🎨 Rich Metadata**: Organize scripts with icons, descriptions, groups, and tags via PowerShell attributes
- **📄 Markdown Output**: Render a script's output as formatted Markdown inside the palette
- **✏️ Open in Editor**: Jump straight to any script's source in your `$EDITOR`/`$VISUAL` (Notepad by default)
- **⚡ Cross-Platform PowerShell**: Supports both PowerShell Core (`pwsh`) and Windows PowerShell (`powershell`)
- **🔒 Security**: Runs in user context with optional admin elevation per script

## 📖 Overview

PaletteShell turns a folder of PowerShell scripts into searchable, runnable commands inside the Windows Command Palette. Each `.ps1` file becomes a list item: PaletteShell reads metadata out of the script (its synopsis, description, icon, parameters, and behavior attributes) and presents it with a friendly title and subtitle. Selecting an item either runs the script immediately or — if the script declares parameters — opens a form to collect input first.

Scripts live in **`Documents\PaletteShellScripts`**. This folder is created automatically the first time the extension loads, and a set of ready-to-use sample scripts plus the supporting `PaletteScriptAttributes.psm1` module are copied in for you. Add, edit, or remove files in that folder at any time; use **"Reload scripts"** in the palette to pick up changes.

## ⚙️ How It Works

### Discovery

When the extension is activated, `PaletteShellExtensionPage` does the following:

1. Creates the `Documents\PaletteShellScripts` directory if it doesn't exist.
2. Copies the embedded sample scripts (only files that aren't already there, so your edits are never overwritten).
3. Copies the `PaletteScriptAttributes.psm1` module and `TextCopy.dll` next to the scripts so they're available at runtime.
4. Enumerates every `*.ps1` file in the folder (top level only) and builds the command list.

The list always begins with four built-in actions:

- **Open scripts folder** — opens `Documents\PaletteShellScripts` in Explorer.
- **Reload scripts** — re-scans the folder so new or changed scripts appear.
- **Create new script** — opens a guided wizard that scaffolds a new `.ps1` with metadata headers.
- **Find more scripts** — opens the community [PaletteShellScripts](https://github.com/paletteshell/PaletteShellScripts) repository in your browser.

Every script item also carries an **Open in editor** context command that opens the source file in your preferred editor.

> ℹ️ New scripts and edits are picked up only when you run **"Reload scripts"** — this is intentional, not a bug.

### Parsing the manifest

For each script, `PowerShellScriptParser` parses the file using the official PowerShell AST parser (`System.Management.Automation.Language.Parser`) — it never executes the script just to read metadata. From the AST it extracts:

- **Title** from the comment-based help `.SYNOPSIS` (falls back to the file name).
- **Description** from `.DESCRIPTION`.
- **Parameters** from the `param()` block, including type, default value, whether it's mandatory, and validation info (`[ValidateSet(...)]` becomes a dropdown, `[ValidateRange(...)]` becomes min/max bounds).
- **Behavior attributes** such as host, working directory, timeout, output mode, icon, environment variables, and elevation (see the [attribute reference](#available-attributes)).
- **Elevation** from either the `[RequiresElevation()]` attribute or the built-in `#Requires -RunAsAdministrator` directive.

### Running a script

Selecting a script item routes to one of three paths, based on its metadata:

- **Has parameters** → opens `ScriptParameterFormPage`, an auto-generated form. Once you submit, the collected values are passed to the script.
- **No parameters, `[ScriptOutput('Markdown')]`** → opens `ScriptMarkdownPage`, which runs the script and renders its stdout as formatted Markdown.
- **No parameters, any other output mode** → runs the script directly via `RunScriptCommand`.

Execution is handled by `ScriptRunner`, which launches `pwsh.exe` (or `powershell.exe`) with `-STA -NoProfile -ExecutionPolicy Bypass`. When the `PaletteScriptAttributes.psm1` module is present alongside the script, the runner imports it and dot-sources the script so the custom attributes resolve and the helper functions (clipboard, logging) are available; the information stream is redirected to stdout so `Write-Host` output is captured.

Whether PaletteShell waits for the script depends on its output mode and timeout:

| Condition | Behavior |
|-----------|----------|
| `[ScriptOutput('None')]` and no `[ScriptTimeout]` | Fire-and-forget — the process is started and a "Script completed" toast is shown. |
| Any other output mode (`Toast`/`Clipboard`/`Markdown`) | PaletteShell waits (up to the declared timeout, or a 30s default), captures stdout/stderr, and surfaces the result. |
| `[ScriptTimeout(ms)]` set | PaletteShell waits up to `ms`, then kills the process tree on timeout. |
| `[ScriptOutput('Clipboard')]` | Captured output is copied to the clipboard. |
| `[ScriptOutput('Markdown')]` | Captured output is rendered as Markdown on its own page. |
| `[RequiresElevation()]` / `#Requires -RunAsAdministrator` | The process is launched elevated (`runas`); output capture is unavailable in this mode. |

### Cross-platform clipboard

The bundled `PaletteScriptAttributes.psm1` module exposes `Get-ClipboardText` / `Set-ClipboardText`, which use the [TextCopy](https://github.com/CopyText/TextCopy) library with a Windows Forms fallback. The host extension also uses TextCopy when copying captured output to the clipboard.

## 🚀 Getting Started

1. Install the extension (from the Microsoft Store, or by building and deploying the MSIX package — see [Building](#-building-from-source)).
2. Open the Command Palette and type **PaletteShell**.
3. Browse the bundled sample scripts, choose **Create new script** to scaffold your own, or **Find more scripts** to grab one from the [community repo](https://github.com/paletteshell/PaletteShellScripts).
4. Edit your scripts in `Documents\PaletteShellScripts` and run **Reload scripts** to see changes.

## ✍️ Creating Your Own Scripts

You can use the in-palette **Create new script** wizard, or simply drop a `.ps1` file into `Documents\PaletteShellScripts`. Scripts use PowerShell attributes for metadata. A typical script looks like:

```powershell
using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Brief description (becomes the command title)
.DESCRIPTION
    Detailed description of what this script does (becomes the subtitle)
.PARAMETER MyParameter
    Parameter description (shown in the input form)
#>
[ScriptHost('pwsh')]
[ScriptCwd('{ScriptDir}')]
[ScriptGroup('Category')]
[ScriptIcon('🎯')]
[ScriptTimeout(15000)]
[ScriptOutput('Clipboard')]
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$MyParameter
)

# Your code here
$result = $MyParameter.ToUpper()
Set-ClipboardText $result
```

### Available Attributes

| Attribute | Purpose |
|-----------|---------|
| `[ScriptHost('pwsh')]` | Host to run under: `'pwsh'` (default) or `'powershell'` |
| `[ScriptCwd('{ScriptDir}')]` | Working directory (supports path tokens, below) |
| `[RequiresElevation()]` | Run the script with administrator rights |
| `[ScriptTimeout(30000)]` | Timeout in milliseconds; also forces wait-and-capture |
| `[ScriptGroup('Category')]` | Group/category name (shown as a tag) |
| `[ScriptIcon('🚀')]` | Icon emoji or glyph shown in the palette |
| `[ScriptOutput('None')]` | Output mode (see below) |
| `[ScriptEnv('VAR', 'value')]` | Set an environment variable (repeat for multiple) |

### Path Tokens

`[ScriptCwd(...)]` and `[ScriptEnv(...)]` values support these tokens, expanded at runtime:

- `{ScriptDir}` — the folder containing the script
- `{Home}` — the current user's profile folder
- `{Temp}` — the system temp folder

### Output Modes

- **None** — run silently; show a "Script completed" toast (default)
- **Clipboard** — copy captured output to the clipboard
- **Toast** — show the captured output in a Windows notification
- **Markdown** — run the script and render its output as formatted Markdown on its own page

### Parameter Form Mapping

Parameters in your `param()` block automatically become form fields:

- `[string]` → text box
- `[int]` / `[double]` → number input (honors `[ValidateRange(min, max)]`)
- `[switch]` / `[bool]` → checkbox
- `[ValidateSet('A','B','C')]` → dropdown
- `[Parameter(Mandatory=$true)]` → required field

### Helper Functions

When you `using module .\PaletteScriptAttributes.psm1`, these functions are available:

```powershell
$text = Get-ClipboardText            # read clipboard (cross-platform)
Set-ClipboardText "Hello World"      # write clipboard (cross-platform)
```

## 📦 Sample Scripts

The extension ships with ready-to-use scripts that double as working examples:

| Script | What it does |
|--------|--------------|
| `Base64-Encode` / `Base64-Decode` | Base64 encode/decode the clipboard text |
| `Clipboard-UrlEncode` / `Clipboard-UrlDecode` | URL encode/decode the clipboard text |
| `Json-Format` / `Json-Minify` | Pretty-print or minify clipboard JSON |
| `Clipboard-ToUpperCase` / `Clipboard-ToLowerCase` | Change the case of clipboard text |
| `Clipboard-SortLines` | Sort the lines on the clipboard |
| `Clipboard-RemoveDuplicateLines` | Remove duplicate lines |
| `Clipboard-TrimLines` | Trim whitespace from each line |
| `Clipboard-ToCSV` | Convert clipboard text to CSV |
| `Text-Transform` | Parameterized text transformation (demonstrates the input form) |
| `Generate-GUID` | Generate a new GUID and copy it to the clipboard |
| `Clipboard-UnixTimestamp` | Insert/convert a Unix timestamp |
| `System-Report` | Render a system information report (demonstrates Markdown output) |

For more, browse the community library at **[paletteshell/PaletteShellScripts](https://github.com/paletteshell/PaletteShellScripts)**.

## 🛠️ Building from Source

PaletteShell is a .NET 9 Windows app packaged as an MSIX Command Palette extension.

**Requirements**

- .NET 9 SDK with the Windows 10.0.26100 platform
- Windows 10 (10.0.19041) or later
- [Windows Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) (PowerToys) installed

**Build**

```powershell
dotnet build PaletteShellExtension/PaletteShellExtension.csproj
```

### Project Structure

| Path | Responsibility |
|------|----------------|
| `PaletteShellExtension.cs` | Extension entry point; provides the commands provider to Command Palette |
| `PaletteShellExtensionCommandsProvider.cs` | Registers the top-level PaletteShell command |
| `Pages/PaletteShellExtensionPage.cs` | Main list page — discovery, sample/module copying, item building |
| `PowerShellScriptParser.cs` | Parses script metadata and parameters from the PowerShell AST |
| `Classes/ScriptManifest.cs`, `ScriptParameter.cs` | Parsed metadata models |
| `Classes/ScriptRunner.cs` | Builds the process and runs scripts (fire-and-forget or wait-and-capture) |
| `Classes/ScriptOutputHandler.cs` | Maps captured output to a result per the script's output mode |
| `Classes/EditorLauncher.cs` | Opens a script in `$VISUAL`/`$EDITOR` (Notepad fallback) |
| `Commands/RunScriptCommand.cs` | Runs a parameterless script and handles output/clipboard/toast |
| `Commands/OpenInEditorCommand.cs`, `OpenFolderCommand.cs`, `OpenLinkCommand.cs`, `ReloadPageCommand.cs` | Built-in and per-item commands |
| `Pages/ScriptParameterFormPage.cs`, `Forms/ScriptParameterForm.cs` | Auto-generated input form for parameterized scripts |
| `Pages/ScriptMarkdownPage.cs` | Runs a script and renders its output as Markdown |
| `Pages/NewScriptWizardPage.cs`, `Forms/NewScriptWizardForm.cs` | "Create new script" scaffolding wizard |
| `PaletteScriptAttributes.psm1` | PowerShell module defining the metadata attributes and clipboard/logging helpers |
| `SampleScripts/` | Embedded sample scripts copied to the user's scripts folder |

## 🤝 Community Scripts

The **[PaletteShellScripts](https://github.com/paletteshell/PaletteShellScripts)** repository is a growing, community-maintained collection of scripts ready to drop into your `Documents\PaletteShellScripts` folder. Grab the ones you find useful, or contribute your own. You can open it any time from the palette's **"Find more scripts"** command.

## Need Help?

Check out the bundled sample scripts (in `Documents\PaletteShellScripts` after first run) and the [community repo](https://github.com/paletteshell/PaletteShellScripts) for examples of common patterns and best practices.

## License

Licensed under the [MIT License](LICENSE).

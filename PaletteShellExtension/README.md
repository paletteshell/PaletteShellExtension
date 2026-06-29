# PaletteShell Extension

**PaletteShell** is a [Windows Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extension that lets you run custom PowerShell scripts directly from the Command Palette. Transform clipboard text, generate GUIDs, format JSON, and automate your daily workflows — all without leaving your keyboard.

## 🌟 Features

- **🚀 Quick Access**: Run PowerShell scripts directly from Windows Command Palette (Win + Alt + Space)
- **📋 Clipboard Utilities**: Transform and manipulate clipboard text with one keystroke
- **🔧 Customizable**: Drop your own `.ps1` files into a folder and they show up automatically
- **📝 Parameter Support**: Scripts with parameters get an interactive input form, generated from the script's own `param()` block
- **🎨 Rich Metadata**: Organize scripts with icons, descriptions, groups, and tags via PowerShell attributes
- **⚡ Cross-Platform PowerShell**: Supports both PowerShell Core (`pwsh`) and Windows PowerShell (`powershell`)
- **🔒 Security**: Runs in user context with optional admin elevation per script

## 📖 Overview

PaletteShell turns a folder of PowerShell scripts into searchable, runnable commands inside the Windows Command Palette. Each `.ps1` file becomes a list item: PaletteShell reads metadata out of the script (its synopsis, description, icon, parameters, and behavior attributes) and presents it with a friendly title and subtitle. Selecting an item either runs the script immediately or — if the script declares parameters — opens a form to collect input first.

Scripts live in **`Documents\PaletteShellScripts`**. This folder is created automatically the first time the extension loads, and a set of ready-to-use [sample scripts](SampleScripts/README.md) plus the supporting `PaletteScriptAttributes.psm1` module are copied in for you. Add, edit, or remove files in that folder at any time; use **"Reload scripts"** in the palette to pick up changes.

## ⚙️ How It Works

### Discovery

When the extension is activated, `PaletteShellExtensionPage` does the following:

1. Creates the `Documents\PaletteShellScripts` directory if it doesn't exist.
2. Copies the embedded sample scripts (only files that aren't already there, so your edits are never overwritten).
3. Copies the `PaletteScriptAttributes.psm1` module and `TextCopy.dll` next to the scripts so they're available at runtime.
4. Enumerates every `*.ps1` file in the folder (top level only) and builds the command list.

The list always begins with three built-in actions:

- **Open scripts folder** — opens `Documents\PaletteShellScripts` in Explorer.
- **Reload scripts** — re-scans the folder so new or changed scripts appear.
- **Create new script** — opens a guided wizard that scaffolds a new `.ps1` with metadata headers.

### Parsing the manifest

For each script, `PowerShellScriptParser` parses the file using the official PowerShell AST parser (`System.Management.Automation.Language.Parser`) — it never executes the script just to read metadata. From the AST it extracts:

- **Title** from the comment-based help `.SYNOPSIS` (falls back to the file name).
- **Description** from `.DESCRIPTION`.
- **Parameters** from the `param()` block, including type, default value, whether it's mandatory, and validation info (`[ValidateSet(...)]` becomes a dropdown, `[ValidateRange(...)]` becomes min/max bounds).
- **Behavior attributes** such as host, working directory, timeout, output mode, icon, environment variables, and elevation (see the [attribute reference](#available-attributes)).
- **Elevation** from either the `[RequiresElevation()]` attribute or the built-in `#Requires -RunAsAdministrator` directive.

### Running a script

- **No parameters** → the item runs the script directly via `RunScriptCommand`.
- **Has parameters** → the item opens `ScriptParameterFormPage`, an auto-generated form. Once you submit, the collected values are passed to the script.

Execution is handled by `ScriptRunner`, which launches `pwsh.exe` (or `powershell.exe`) with `-STA -NoProfile -ExecutionPolicy Bypass`. When the `PaletteScriptAttributes.psm1` module is present alongside the script, the runner imports it and dot-sources the script so the custom attributes resolve and the helper functions (clipboard, logging) are available; the information stream is redirected to stdout so `Write-Host` output is captured.

Behavior depends on the script's metadata:

| Condition | Behavior |
|-----------|----------|
| No `[ScriptTimeout]` | Fire-and-forget — the process is started and PaletteShell shows a "Script Completed" toast. |
| `[ScriptTimeout(ms)]` set | PaletteShell waits up to `ms`, captures stdout/stderr, and surfaces the result (or kills the process tree on timeout). |
| `[ScriptOutput('Clipboard')]` | Captured output is copied to the clipboard. |
| `[RequiresElevation()]` / `#Requires -RunAsAdministrator` | The process is launched elevated (`runas`); output capture is unavailable in this mode. |

### Cross-platform clipboard

The bundled `PaletteScriptAttributes.psm1` module exposes `Get-ClipboardText` / `Set-ClipboardText`, which use the [TextCopy](https://github.com/CopyText/TextCopy) library with a Windows Forms fallback. The host extension also uses TextCopy when copying captured output to the clipboard.

## 🚀 Getting Started

1. Install the extension (from the Microsoft Store, or by building and deploying the MSIX package — see [Building](#-building-from-source)).
2. Open the Command Palette and type **PaletteShell**.
3. Browse the bundled sample scripts, or choose **Create new script** to scaffold your own.
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
| `[ScriptTimeout(30000)]` | Timeout in milliseconds; also enables output capture |
| `[ScriptMutex('lock-name')]` | Named lock to prevent concurrent execution |
| `[ScriptGroup('Category')]` | Group/category name |
| `[ScriptIcon('🚀')]` | Icon emoji or glyph shown in the palette |
| `[ScriptOutput('None')]` | Output mode (see below) |
| `[ScriptEnv('VAR', 'value')]` | Set an environment variable (repeat for multiple) |

### Path Tokens

`[ScriptCwd(...)]` and `[ScriptEnv(...)]` values support these tokens, expanded at runtime:

- `{ScriptDir}` — the folder containing the script
- `{Home}` — the current user's profile folder
- `{Temp}` — the system temp folder

### Output Modes

- **None** — display output in the Command Palette (default)
- **Clipboard** — copy captured output to the clipboard
- **Toast** — show a Windows notification
- **Markdown** — display formatted markdown

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

Enable-PaletteLogging                # log to %TEMP%\PaletteShell-{date}.log
Write-PaletteLog "message" -Level Info
```

Logging can also be enabled by setting the `PALETTESHELL_LOGGING` environment variable to `1` or `true`.

## 📦 Sample Scripts

The extension ships with ready-to-use scripts covering clipboard transforms (Base64, URL encode/decode, JSON format/minify, case conversion, sort/dedupe lines, CSV), utilities (GUID and Unix timestamp generation), and system info. See [SampleScripts/README.md](SampleScripts/README.md) for the full list.

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

To produce the MSIX package, use the **Package and Publish** workflow in Visual Studio, or publish with the platform-specific profile (`win-x64` / `win-arm64`). Once deployed and registered, the extension appears in the Command Palette automatically.

### Project Structure

| Path | Responsibility |
|------|----------------|
| `PaletteShellExtension.cs` | Extension entry point; provides the commands provider to Command Palette |
| `Pages/PaletteShellExtensionPage.cs` | Main list page — discovery, sample/module copying, item building |
| `PowerShellScriptParser.cs` | Parses script metadata and parameters from the PowerShell AST |
| `Classes/ScriptManifest.cs`, `ScriptParameter.cs` | Parsed metadata models |
| `Classes/ScriptRunner.cs` | Builds the process and runs scripts (fire-and-forget or wait-and-capture) |
| `Commands/RunScriptCommand.cs` | Runs a parameterless script and handles output/clipboard/toast |
| `Pages/ScriptParameterFormPage.cs`, `Forms/ScriptParameterForm.cs` | Auto-generated input form for parameterized scripts |
| `Pages/NewScriptWizardPage.cs`, `Forms/NewScriptWizardForm.cs` | "Create new script" scaffolding wizard |
| `PaletteScriptAttributes.psm1` | PowerShell module defining the metadata attributes and clipboard/logging helpers |
| `SampleScripts/` | Embedded sample scripts copied to the user's scripts folder |

## Need Help?

Check out the existing [sample scripts](SampleScripts/README.md) for examples of common patterns and best practices!

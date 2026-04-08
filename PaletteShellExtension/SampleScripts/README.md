# PaletteShell Sample Scripts

This folder contains sample PowerShell scripts that demonstrate the capabilities of PaletteShell.

## 📋 Clipboard Utilities

### Sample-ClipboardToCSV.ps1
Convert multiline clipboard text to comma-delimited, quote-wrapped CSV format.

**Example:** Convert a list of names into a CSV-ready format.

### Sample-Base64Encode.ps1
Encode clipboard text to Base64 format.

**Example:** Encode credentials or binary data for safe transmission.

### Sample-Base64Decode.ps1
Decode Base64 text from clipboard back to plain text.

**Example:** Decode Base64-encoded configuration values.

### Sample-UrlEncode.ps1
URL encode clipboard text for safe use in URLs.

**Example:** Encode query parameters with special characters.

### Sample-UrlDecode.ps1
Decode URL-encoded text from clipboard.

**Example:** Make encoded URLs readable.

### Sample-JsonFormat.ps1
Pretty-print JSON from clipboard with proper indentation.

**Example:** Make minified JSON readable for debugging.

### Sample-JsonMinify.ps1
Compress JSON by removing whitespace and indentation.

**Example:** Reduce JSON size before transmission.

### Sample-TrimLines.ps1
Remove leading and trailing whitespace from each line.

**Example:** Clean up pasted code or formatted text.

### Sample-SortLines.ps1
Sort clipboard lines alphabetically.

**Example:** Organize lists, imports, or configuration entries.

### Sample-RemoveDuplicateLines.ps1
Remove duplicate lines from clipboard text.

**Example:** Clean up redundant entries in lists.

### Sample-UpperCase.ps1
Convert clipboard text to UPPERCASE.

**Example:** Format constant names or SQL keywords.

### Sample-LowerCase.ps1
Convert clipboard text to lowercase.

**Example:** Normalize identifiers or URLs.

## 🔧 Utilities

### Sample-GenerateGUID.ps1
Generate a new GUID/UUID and copy to clipboard.

**Example:** Create unique identifiers for database records or API keys.

### Sample-UnixTimestamp.ps1
Generate the current Unix timestamp.

**Example:** Get timestamps for logging or API calls.

## 💻 System

### Sample-SystemInfo.ps1
Display comprehensive system information including OS, hardware, memory, and disks.

**Example:** Quick system diagnostics and inventory.

---

## Creating Your Own Scripts

To create a new script:
1. Open PaletteShell in Command Palette
2. Select "Create new script..."
3. Choose a template and name your script
4. Add your PowerShell code

### Script Metadata with Attributes

Scripts use PowerShell attributes for metadata. Start your script with:

```powershell
using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Brief description
.DESCRIPTION
    Detailed description of what this script does
.PARAMETER ParamName
    Parameter description
#>
[ScriptHost('pwsh')]
[ScriptCwd('{ScriptDir}')]
[ScriptGroup('Category')]
[ScriptIcon('🎯')]
[ScriptTimeout(15000)]
[ScriptOutput('None')]
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$MyParameter
)

# Your code here
```

### Available Attributes

- **`[ScriptHost('pwsh')]`** - Host: 'pwsh' or 'powershell'
- **`[ScriptCwd('{ScriptDir}')]`** - Working directory
- **`[RequiresElevation()]`** - Requires admin rights
- **`[ScriptTimeout(30000)]`** - Timeout in milliseconds
- **`[ScriptMutex('lock-name')]`** - Prevent concurrent execution
- **`[ScriptGroup('Category')]`** - Group/category name
- **`[ScriptIcon('🚀')]`** - Icon emoji
- **`[ScriptOutput('None')]`** - Output mode (see below)
- **`[ScriptEnv('VAR', 'value')]`** - Environment variable (repeat for multiple)

### Available Output Modes
- **None**: Display output in Command Palette
- **Clipboard**: Copy result to clipboard
- **Toast**: Show Windows notification
- **Markdown**: Display formatted markdown

### Cross-Platform Clipboard Functions

Use these functions from the `PaletteScriptAttributes.psm1` module:

```powershell
# Get clipboard text
$text = Get-ClipboardText

# Set clipboard text
Set-ClipboardText "Hello World"
```

These functions work across Windows, macOS, and Linux using the TextCopy library.

## Need Help?

Check out the existing sample scripts for examples of common patterns and best practices!

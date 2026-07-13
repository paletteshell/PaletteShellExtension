# PaletteScriptAttributes.psm1
# Custom attributes for PaletteShell script metadata

using namespace System
using namespace System.Management.Automation

# Script execution host
class ScriptHostAttribute : Attribute {
    [string]$Host
    ScriptHostAttribute([string]$host) { $this.Host = $host }
}

# Working directory
class ScriptCwdAttribute : Attribute {
    [string]$Path
    ScriptCwdAttribute([string]$path) { $this.Path = $path }
}

# Requires elevation (can also use built-in #Requires -RunAsAdministrator)
class RequiresElevationAttribute : Attribute {}

# Prompt for confirmation before running. Pairs with RequiresElevation to gate
# destructive scripts, e.g. [ConfirmBeforeRun('This permanently deletes files')].
class ConfirmBeforeRunAttribute : Attribute {
    [string]$Message
    ConfirmBeforeRunAttribute() { $this.Message = '' }
    ConfirmBeforeRunAttribute([string]$message) { $this.Message = $message }
}

# Script timeout in milliseconds
class ScriptTimeoutAttribute : Attribute {
    [int]$Milliseconds
    ScriptTimeoutAttribute([int]$ms) { $this.Milliseconds = $ms }
}

# Output handling
class ScriptOutputAttribute : Attribute {
    # None, Toast, Clipboard, Markdown, Result, List, Open, or File.
    #
    # Two dispositions:
    #   Fire-and-forget (the palette closes and a completion banner is shown when done):
    #     None      - run silently; report completion.
    #     Toast     - show stdout in the completion banner.
    #     Clipboard - copy stdout to the clipboard.
    #     Open      - launch the first non-empty stdout line as a URL, file, or folder path.
    #     File      - write stdout to a temp file and open it in the editor; append an
    #                 extension hint after a colon to set the file type, e.g. 'File:csv' or 'File:json'.
    #   In-palette (the palette stays open on a page that renders the output):
    #     Result    - show stdout as a single copyable result (Enter copies; a "Run again"
    #                 command regenerates) — like a calculator answer; good for generators.
    #     Markdown  - render stdout as Markdown.
    #     List      - parse stdout (newline-delimited, or a JSON array) into a searchable
    #                 results page where each line/object becomes a selectable item.
    [string]$Mode
    ScriptOutputAttribute([string]$mode) { $this.Mode = $mode }
}

# Script group/category
class ScriptGroupAttribute : Attribute {
    [string]$Name
    ScriptGroupAttribute([string]$name) { $this.Name = $name }
}

# Free-form tags, comma-delimited (e.g. [ScriptTags('network,dns,admin')]). Used by tooling
# such as the Script Manager's catalog browser.
class ScriptTagsAttribute : Attribute {
    [string]$Tags
    ScriptTagsAttribute([string]$tags) { $this.Tags = $tags }
}

# Script version (recommended: SemVer, e.g. '1.0.0'). Lets tools detect when a newer copy is available.
class ScriptVersionAttribute : Attribute {
    [string]$Version
    ScriptVersionAttribute([string]$version) { $this.Version = $version }
}

# Requires a PowerShell module be installed to run this script. Repeat the attribute for
# multiple modules, e.g. [RequiresModule('ImportExcel')] [RequiresModule('Az.Accounts')].
# When the script is run, PaletteShell checks each module and fails with an Install-Module
# hint if it isn't available.
class RequiresModuleAttribute : Attribute {
    [string]$Name
    RequiresModuleAttribute([string]$name) { $this.Name = $name }
}

# Minimum PaletteShell app version (SemVer) required to run this script. PaletteShell hides
# the script (with an explanatory row) instead of running it when the installed app is older.
class RequiresPaletteShellMinimumAttribute : Attribute {
    [string]$Version
    RequiresPaletteShellMinimumAttribute([string]$version) { $this.Version = $version }
}

# Maximum PaletteShell app version (SemVer) this script still works on - for a script that
# depends on behavior later removed or changed. PaletteShell hides the script (with an
# explanatory row) instead of running it when the installed app is newer. Optional; most scripts
# should omit this and only set RequiresPaletteShellMinimum.
class RequiresPaletteShellMaximumAttribute : Attribute {
    [string]$Version
    RequiresPaletteShellMaximumAttribute([string]$version) { $this.Version = $version }
}

# Icon emoji or glyph
class ScriptIconAttribute : Attribute {
    [string]$Icon
    ScriptIconAttribute([string]$icon) { $this.Icon = $icon }
}

# Parameter-level: pass the form value through verbatim for PowerShell to evaluate,
# instead of the default of passing it as a literal string.
class AllowExpressionAttribute : Attribute {}

# Environment variables (key=value format)
class ScriptEnvAttribute : Attribute {
    [string]$Name
    [string]$Value
    ScriptEnvAttribute([string]$name, [string]$value) {
        $this.Name = $name
        $this.Value = $value
    }
}

# Clipboard functions: built-in Get/Set-Clipboard first, Windows Forms as fallback.
function Get-ClipboardText {
    <#
    .SYNOPSIS
        Gets text from the clipboard.
    .DESCRIPTION
        Uses the built-in Get-Clipboard cmdlet (PowerShell 5+) with a Windows Forms fallback.
    #>

    # Use built-in Get-Clipboard if available (PowerShell 5+)
    if (Get-Command -Name Get-Clipboard -ErrorAction SilentlyContinue) {
        # PowerShell 7+ (Core) Get-Clipboard is text-only and has no -Format parameter.
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            return Get-Clipboard -Raw
        }
        return Get-Clipboard -Format Text -Raw
    }

    # Fallback to Windows Forms
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        return [System.Windows.Forms.Clipboard]::GetText()
    }
    catch {
        throw "Unable to access clipboard. Error: $($_.Exception.Message)"
    }
}

function Set-ClipboardText {
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [AllowEmptyString()]
        [string]$Text
    )

    $ErrorActionPreference = 'Continue'

    # Try built-in Set-Clipboard first (PowerShell 5+)p
    try {
        $hasSetClipboard = Get-Command -Name Set-Clipboard -ErrorAction SilentlyContinue
        if ($hasSetClipboard) {
            Set-Clipboard -Value $Text
            return
        }
    }
    catch {
        # Built-in Set-Clipboard failed; fall through to the next method.
    }

    # Fallback to Windows Forms
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        [System.Windows.Forms.Clipboard]::SetText($Text)
    }
    catch {
        throw "Unable to set clipboard. Error: $($_.Exception.Message)"
    }
}

Export-ModuleMember -Variable * -Function * -Cmdlet *

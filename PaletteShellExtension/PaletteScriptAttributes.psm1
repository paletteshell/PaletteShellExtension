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

# Script timeout in milliseconds
class ScriptTimeoutAttribute : Attribute {
    [int]$Milliseconds
    ScriptTimeoutAttribute([int]$ms) { $this.Milliseconds = $ms }
}

# Mutual exclusion lock name
class ScriptMutexAttribute : Attribute {
    [string]$Name
    ScriptMutexAttribute([string]$name) { $this.Name = $name }
}

# Output handling
class ScriptOutputAttribute : Attribute {
    [string]$Mode  # None, Clipboard, Markdown, Toast
    ScriptOutputAttribute([string]$mode) { $this.Mode = $mode }
}

# Output action
class ScriptOutputActionAttribute : Attribute {
    [string]$Action  # None, OpenPath
    ScriptOutputActionAttribute([string]$action) { $this.Action = $action }
}

# Script group/category
class ScriptGroupAttribute : Attribute {
    [string]$Name
    ScriptGroupAttribute([string]$name) { $this.Name = $name }
}

# Icon emoji or glyph
class ScriptIconAttribute : Attribute {
    [string]$Icon
    ScriptIconAttribute([string]$icon) { $this.Icon = $icon }
}

# Environment variables (key=value format)
class ScriptEnvAttribute : Attribute {
    [string]$Name
    [string]$Value
    ScriptEnvAttribute([string]$name, [string]$value) {
        $this.Name = $name
        $this.Value = $value
    }
}

# Cross-platform clipboard functions using TextCopy
function Get-ClipboardText {
    <#
    .SYNOPSIS
        Gets text from the clipboard in a cross-platform way.
    .DESCRIPTION
        Uses TextCopy library for cross-platform clipboard access with Windows Forms fallback.
    #>

    # Use built-in Get-Clipboard if available (PowerShell 5+)
    if (Get-Command -Name Get-Clipboard -ErrorAction SilentlyContinue) {
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
    <#
    .SYNOPSIS
        Sets text to the clipboard in a cross-platform way.
    .PARAMETER Text
        The text to copy to the clipboard.
    .DESCRIPTION
        Uses TextCopy library for cross-platform clipboard access with Windows Forms fallback.
    #>
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [AllowEmptyString()]
        [string]$Text
    )

    $ErrorActionPreference = 'Continue'

    # Try built-in Set-Clipboard first (PowerShell 5+)
    
    try {
        $hasSetClipboard = Get-Command -Name Set-Clipboard -ErrorAction SilentlyContinue
        if ($hasSetClipboard) {
    

            Set-Clipboard -Value $Text
            return
        }
    }
    catch {
        
    }

    # Try TextCopy
    try {
        $textCopyDll = Join-Path $PSScriptRoot 'TextCopy.dll'
        
        if (Test-Path $textCopyDll) {
            $loaded = [AppDomain]::CurrentDomain.GetAssemblies() | 
                Where-Object { $_.GetName().Name -eq 'TextCopy' } | 
                Select-Object -First 1

            if (-not $loaded) {
                $assembly = [System.Reflection.Assembly]::LoadFrom($textCopyDll)
            }
            else {
            }

         
            [TextCopy.ClipboardService]::SetText($Text)
            return
        }
        else {
        }
    }
    catch {
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

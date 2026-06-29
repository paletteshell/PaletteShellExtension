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
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [AllowEmptyString()]
        [string]$Text
    )

    Write-PaletteLog "Setting clipboard text (length: $($Text.Length))" -Level Debug

    $ErrorActionPreference = 'Continue'

    # Try built-in Set-Clipboard first (PowerShell 5+)p
    try {
        $hasSetClipboard = Get-Command -Name Set-Clipboard -ErrorAction SilentlyContinue
        if ($hasSetClipboard) {
            Set-Clipboard -Value $Text
            Write-PaletteLog "Clipboard set using built-in Set-Clipboard" -Level Debug
            return
        }
    }
    catch {
        Write-PaletteLog "Built-in Set-Clipboard failed" -Level Warning -Exception $_.Exception
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
                Write-PaletteLog "Loaded TextCopy.dll from $textCopyDll" -Level Debug
            }

            [TextCopy.ClipboardService]::SetText($Text)
            Write-PaletteLog "Clipboard set using TextCopy" -Level Debug
            return
        }
    }
    catch {
        Write-PaletteLog "TextCopy failed" -Level Warning -Exception $_.Exception
    }

    # Fallback to Windows Forms
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        [System.Windows.Forms.Clipboard]::SetText($Text)
        Write-PaletteLog "Clipboard set using Windows Forms" -Level Debug
    }
    catch {
        Write-PaletteLog "All clipboard methods failed" -Level Error -Exception $_.Exception
        throw "Unable to set clipboard. Error: $($_.Exception.Message)"
    }
}

# Script-level variable for log file path
$script:LogFilePath = $null
$script:LoggingEnabled = $false

function Enable-PaletteLogging {
    <#
    .SYNOPSIS
        Enables logging to a file.
    .PARAMETER LogPath
        Path to the log file. If not specified, uses %TEMP%\PaletteShell-{date}.log
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$LogPath
    )

    if ([string]::IsNullOrWhiteSpace($LogPath)) {
        $tempPath = [System.IO.Path]::GetTempPath()
        $dateStamp = Get-Date -Format 'yyyyMMdd'
        $LogPath = Join-Path $tempPath "PaletteShell-$dateStamp.log"
    }

    $script:LogFilePath = $LogPath
    $script:LoggingEnabled = $true

    Write-PaletteLog "Logging enabled. Log file: $LogPath" -Level Info
}

function Disable-PaletteLogging {
    <#
    .SYNOPSIS
        Disables logging to a file.
    #>
    Write-PaletteLog "Logging disabled." -Level Info
    $script:LoggingEnabled = $false
}

function Write-PaletteLog {
    <#
    .SYNOPSIS
        Writes a log entry to the log file if logging is enabled.
    .PARAMETER Message
        The message to log.
    .PARAMETER Level
        Log level: Debug, Info, Warning, Error. Default is Info.
    .PARAMETER Exception
        Optional exception object to log.
    #>
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$Message,

        [Parameter(Mandatory=$false)]
        [ValidateSet('Debug', 'Info', 'Warning', 'Error')]
        [string]$Level = 'Info',

        [Parameter(Mandatory=$false)]
        [System.Exception]$Exception
    )

    # Check if logging is enabled via environment variable
    if (-not $script:LoggingEnabled) {
        $envEnabled = [System.Environment]::GetEnvironmentVariable('PALETTESHELL_LOGGING')
        if ($envEnabled -eq '1' -or $envEnabled -eq 'true') {
            Enable-PaletteLogging
        }
        else {
            return
        }
    }

    if (-not $script:LoggingEnabled -or [string]::IsNullOrWhiteSpace($script:LogFilePath)) {
        return
    }

    try {
        $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
        $processId = [System.Diagnostics.Process]::GetCurrentProcess().Id
        $threadId = [System.Threading.Thread]::CurrentThread.ManagedThreadId
        
        $logEntry = "[$timestamp] [PID:$processId] [TID:$threadId] [$Level] $Message"

        if ($Exception) {
            $logEntry += "`n  Exception: $($Exception.GetType().FullName): $($Exception.Message)"
            $logEntry += "`n  StackTrace: $($Exception.StackTrace)"
        }

        # Thread-safe file append
        $mutex = New-Object System.Threading.Mutex($false, 'Global\PaletteShellLog')
        try {
            $mutex.WaitOne() | Out-Null
            Add-Content -Path $script:LogFilePath -Value $logEntry -Encoding UTF8
        }
        finally {
            $mutex.ReleaseMutex()
            $mutex.Dispose()
        }
    }
    catch {
        # Silently fail - don't break script execution due to logging issues
        Write-Warning "Failed to write to log: $($_.Exception.Message)"
    }
}

function Get-PaletteLogPath {
    <#
    .SYNOPSIS
        Gets the current log file path.
    #>
    return $script:LogFilePath
}

Export-ModuleMember -Variable * -Function * -Cmdlet *

using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Get Unix Timestamp
.DESCRIPTION
    Generate current Unix timestamp and copy to clipboard
#>
[ScriptHost('pwsh')]
[ScriptGroup('Utilities')]
[ScriptIcon('⏰')]
[ScriptTimeout(5000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
Set-ClipboardText $timestamp.ToString()
Write-Host "Unix Timestamp: $timestamp copied to clipboard"

using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Remove Duplicate Lines
.DESCRIPTION
    Remove duplicate lines from clipboard text
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('🔍')]
[ScriptTimeout(10000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "Clipboard is empty"
    exit 1
}

$lines = $text -split "(\r\n|\n)" | Where-Object { $_ -match '\S' }
$originalCount = $lines.Count
$unique = $lines | Select-Object -Unique
$result = $unique -join [Environment]::NewLine
Set-ClipboardText $result
Write-Host "Original: $originalCount lines → Unique: $($unique.Count) lines"

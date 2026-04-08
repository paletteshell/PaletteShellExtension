using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Sort Lines in Clipboard
.DESCRIPTION
    Sort clipboard lines alphabetically
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('🔤')]
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
$sorted = $lines | Sort-Object
$result = $sorted -join [Environment]::NewLine
Set-ClipboardText $result
Write-Host "Sorted $($lines.Count) lines alphabetically"

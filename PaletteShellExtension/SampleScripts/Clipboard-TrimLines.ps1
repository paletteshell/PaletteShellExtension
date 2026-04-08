using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Trim Lines in Clipboard
.DESCRIPTION
    Remove leading and trailing whitespace from each line
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('✂️')]
[ScriptTimeout(10000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "Clipboard is empty"
    exit 1
}

$lines = $text -split "(\r\n|\n)"
$trimmed = $lines | ForEach-Object { $_.Trim() }
$result = $trimmed -join [Environment]::NewLine
Set-ClipboardText $result
Write-Host "Trimmed whitespace from $($lines.Count) lines"

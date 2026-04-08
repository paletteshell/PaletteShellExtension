using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Convert to lowercase
.DESCRIPTION
    Convert clipboard text to lowercase
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('🔡')]
[ScriptTimeout(5000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "Clipboard is empty"
    exit 1
}

$lower = $text.ToLower()
Set-ClipboardText $lower
Write-Host "Converted to lowercase and copied to clipboard"

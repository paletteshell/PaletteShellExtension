using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Convert to UPPERCASE
.DESCRIPTION
    Convert clipboard text to uppercase
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('🔠')]
[ScriptTimeout(5000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "Clipboard is empty"
    exit 1
}

$upper = $text.ToUpper()
Set-ClipboardText $upper
Write-Host "Converted to uppercase and copied to clipboard"

using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    URL Encode Clipboard
.DESCRIPTION
    URL encode clipboard text and copy back
#>
[ScriptHost('auto')]
[ScriptGroup('Clipboard')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🌐')]
[ScriptTimeout(10000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "Clipboard is empty"
    exit 1
}

$encoded = [System.Uri]::EscapeDataString($text)
Set-ClipboardText $encoded
Write-Host "Copied to clipboard"

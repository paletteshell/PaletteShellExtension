using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    URL Decode Clipboard
.DESCRIPTION
    URL decode clipboard text and copy back
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🔗')]
[ScriptTimeout(10000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "Clipboard is empty"
    exit 1
}

# Use Uri.UnescapeDataString instead of HttpUtility.UrlDecode
# Also handle '+' to space conversion for form-encoded data
$decoded = [System.Uri]::UnescapeDataString($text.Replace('+', ' '))
Set-ClipboardText $decoded
Write-Host "Copied to clipboard"
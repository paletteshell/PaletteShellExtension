using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Base64 Encode Clipboard
.DESCRIPTION
    Encode clipboard text to Base64 and copy back
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('🔐')]
[ScriptTimeout(10000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "Clipboard is empty"
    exit 1
}

$bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
$base64 = [System.Convert]::ToBase64String($bytes)
Set-ClipboardText $base64
Write-Host "Encoded $($text.Length) characters to Base64 and copied to clipboard"
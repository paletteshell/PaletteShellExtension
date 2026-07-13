using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Base64 Decode Clipboard
.DESCRIPTION
    Decode Base64 clipboard text and copy back
#>
[ScriptHost('auto')]
[ScriptGroup('Clipboard')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🔓')]
[ScriptTimeout(10000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "Clipboard is empty"
    exit 1
}

try {
    $bytes = [System.Convert]::FromBase64String($text)
    $decoded = [System.Text.Encoding]::UTF8.GetString($bytes)
    Set-ClipboardText $decoded
    Write-Host "Decoded Base64 to $($decoded.Length) characters and copied to clipboard"
}
catch {
    Write-Host "Invalid Base64 string"
    exit 1
}

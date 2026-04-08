using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Format JSON Clipboard
.DESCRIPTION
    Pretty-print JSON from clipboard and copy back
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('📄')]
[ScriptTimeout(10000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text = Get-ClipboardText
if ([string]::IsNullOrEmpty($text)) {
    Write-Host "⚠️ Clipboard is empty"
    exit 1
}

try {
    $json = $text | ConvertFrom-Json
    $formatted = $json | ConvertTo-Json -Depth 100
    Set-ClipboardText $formatted
    Write-Host "Formatted JSON and copied to clipboard"
}
catch {
    Write-Host "Invalid JSON: $($_.Exception.Message)"
    exit 1
}

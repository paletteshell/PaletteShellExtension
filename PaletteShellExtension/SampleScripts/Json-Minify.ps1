using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Minify JSON Clipboard
.DESCRIPTION
    Compress JSON from clipboard and copy back
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('🗜️')]
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
    $json = $text | ConvertFrom-Json
    $minified = $json | ConvertTo-Json -Depth 100 -Compress
    Set-ClipboardText $minified
    Write-Host "Saved $($text.Length - $minified.Length) characters ($([math]::Round((($text.Length - $minified.Length) / $text.Length) * 100, 1))% reduction)"
}
catch {
    Write-Host "Invalid JSON: $($_.Exception.Message)"
    exit 1
}

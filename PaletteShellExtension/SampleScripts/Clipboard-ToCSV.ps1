using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Clipboard → CSV (quoted)
.DESCRIPTION
    Convert multiline clipboard text to comma-delimited, quote-wrapped CSV
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('📋')]
[ScriptTimeout(15000)]
[ScriptOutput('None')]
[CmdletBinding()]
param()

$text  = Get-ClipboardText
$lines = $text -split "(\r\n|\n|\r)" | Where-Object { $_ -ne "" }
$csv   = ($lines | ForEach-Object { '"' + ($_ -replace '"','""') + '"' }) -join ','
Set-ClipboardText $csv
Write-Host "Converted $($lines.Count) lines to CSV and copied to clipboard"

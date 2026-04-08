using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Generate GUID
.DESCRIPTION
    Generate a new GUID and copy to clipboard
#>
[ScriptHost('pwsh')]
[ScriptGroup('Utilities')]
[ScriptIcon('🆔')]
[ScriptTimeout(5000)]
[ScriptOutput('None')]
param()

$guid = [System.Guid]::NewGuid().ToString()

Write-Host "Generated GUID: $guid"

Set-ClipboardText $guid
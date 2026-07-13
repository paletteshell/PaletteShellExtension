using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Attr: ScriptHost pwsh
.DESCRIPTION
    Forces the PowerShell 7 host. EXPECT: runs under pwsh.exe and reports PSVersion 7.x
    with PSEdition 'Core'. If pwsh is not installed, EXPECT a clear "PowerShell 7 is
    required but not installed" error (not a silent fallback).
#>
[ScriptHost('pwsh')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,host')]
[ScriptVersion('1.0.0')]
[ScriptIcon('⚡')]
[ScriptOutput('Markdown')]
param()

Write-Host "# Host: PowerShell 7 (pwsh)"
Write-Host ""
Write-Host "- PSVersion: ``$($PSVersionTable.PSVersion)``"
Write-Host "- PSEdition: ``$($PSVersionTable.PSEdition)`` (expected: Core)"

using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Attr: ScriptHost powershell
.DESCRIPTION
    Forces the Windows PowerShell host. EXPECT: runs under powershell.exe and reports
    PSVersion 5.x with PSEdition 'Desktop'. Compare with Test-Host-Pwsh (7.x / 'Core').
#>
[ScriptHost('powershell')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,host')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🪟')]
[ScriptOutput('Markdown')]
param()

Write-Host "# Host: Windows PowerShell"
Write-Host ""
Write-Host "- PSVersion: ``$($PSVersionTable.PSVersion)``"
Write-Host "- PSEdition: ``$($PSVersionTable.PSEdition)`` (expected: Desktop)"

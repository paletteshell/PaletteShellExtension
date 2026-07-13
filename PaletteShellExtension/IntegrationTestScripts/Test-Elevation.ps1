using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Elevation: RequiresElevation
.DESCRIPTION
    EXPECT: running this triggers a UAC elevation prompt. Because elevated runs cannot have
    their stdout captured, the palette reports completion without surfacing output. To
    confirm it actually elevated, this writes a marker file to TEMP and reports admin state
    there (open %TEMP%\PaletteShell-IT-Elevation.txt).
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,elevation')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🛡️')]
[ScriptOutput('None')]
[RequiresElevation()]
[CmdletBinding()]
param()

$id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object System.Security.Principal.WindowsPrincipal($id)
$isAdmin = $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)

$out = Join-Path $env:TEMP 'PaletteShell-IT-Elevation.txt'
"IsAdmin=$isAdmin  User=$($id.Name)  At=$(Get-Date -Format o)" | Set-Content -LiteralPath $out
Write-Host "IsAdmin=$isAdmin (see $out)"

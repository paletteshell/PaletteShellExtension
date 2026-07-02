using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Restart Explorer
.DESCRIPTION
    Restart the Windows Explorer shell — handy after changing shell settings. Open File
    Explorer windows will close, so it asks for confirmation first (demonstrates
    ConfirmBeforeRun).
#>
[ScriptHost('pwsh')]
[ScriptGroup('System')]
[ScriptIcon('🔁')]
[ScriptTimeout(10000)]
[ScriptOutput('Toast')]
[ConfirmBeforeRun('This restarts Windows Explorer. Open File Explorer windows will close.')]
param()

Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Windows usually relaunches the shell on its own; start it if it didn't come back.
if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) {
    Start-Process explorer.exe
}

Write-Host 'Explorer restarted'

using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Confirm: before run (no params)
.DESCRIPTION
    EXPECT: selecting this script shows a confirmation dialog with the custom message
    below BEFORE anything runs. Cancel => nothing runs. Confirm => the marker is written.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,confirm')]
[ScriptVersion('1.0.0')]
[ScriptIcon('⚠️')]
[ScriptOutput('Toast')]
[ConfirmBeforeRun('Integration test: confirm to proceed (nothing destructive happens).')]
param()

Write-Output "Confirmed and ran at $(Get-Date -Format o)"

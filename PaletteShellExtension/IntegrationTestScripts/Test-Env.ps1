using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Attr: ScriptEnv
.DESCRIPTION
    Declares two [ScriptEnv] variables. EXPECT: both are present in the process
    environment and echoed below with their declared values.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,env')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🌱')]
[ScriptOutput('Markdown')]
[ScriptEnv('PS_IT_ONE', 'first-value')]
[ScriptEnv('PS_IT_TWO', 'second-value')]
[CmdletBinding()]
param()

Write-Host "# ScriptEnv Test"
Write-Host ""
Write-Host "| Variable | Expected | Actual |"
Write-Host "| --- | --- | --- |"
Write-Host "| PS_IT_ONE | first-value  | ``$env:PS_IT_ONE`` |"
Write-Host "| PS_IT_TWO | second-value | ``$env:PS_IT_TWO`` |"

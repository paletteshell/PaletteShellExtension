using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: Markdown
.DESCRIPTION
    EXPECT: stdout rendered as Markdown (heading + table below appear formatted, not raw).
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output')]
[ScriptVersion('1.0.0')]
[ScriptIcon('📝')]
[ScriptOutput('Markdown')]
param()

Write-Host "# Markdown Output Test"
Write-Host ""
Write-Host "If you can read a **bold** word and the table below is rendered, Markdown mode works."
Write-Host ""
Write-Host "| Key | Value |"
Write-Host "| --- | --- |"
Write-Host "| Time | $(Get-Date -Format o) |"
Write-Host "| PID  | $PID |"

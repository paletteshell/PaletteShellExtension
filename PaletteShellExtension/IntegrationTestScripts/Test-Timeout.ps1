using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Timeout: enforced kill
.DESCRIPTION
    Declares a 2s [ScriptTimeout] but sleeps 30s. EXPECT: the run is terminated at ~2s and
    surfaced as a timeout failure — it must NOT run to completion or report success.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,timeout')]
[ScriptVersion('1.0.0')]
[ScriptIcon('⏱️')]
[ScriptTimeout(2000)]
[ScriptOutput('Toast')]
[CmdletBinding()]
param()

Write-Host "Sleeping 30s — should be killed at ~2s by ScriptTimeout(2000)."
Start-Sleep -Seconds 30
Write-Host "THIS LINE MUST NOT APPEAR — timeout was not enforced."

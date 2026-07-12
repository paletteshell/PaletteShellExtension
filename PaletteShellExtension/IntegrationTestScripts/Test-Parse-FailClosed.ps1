using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Parse: fail-closed (bad metadata)
.DESCRIPTION
    Intentionally malformed metadata: ScriptTimeout is given a non-numeric value, which is a
    critical attribute the parser cannot honor. EXPECT: the parser FAILS CLOSED — this script
    is shown as a disabled "repair" row (subtitle explains the bad ScriptTimeout value) and
    must NOT be runnable. This guards the "don't silently run partially-understood metadata"
    behavior.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,parse')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🧨')]
[ScriptTimeout(not-a-number)]
[ScriptOutput('Toast')]
[CmdletBinding()]
param()

Write-Host "If you can run this, fail-closed parsing did not trigger."

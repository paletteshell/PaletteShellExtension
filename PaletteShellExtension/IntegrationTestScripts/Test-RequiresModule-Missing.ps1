using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Attr: RequiresModule (missing)
.DESCRIPTION
    Declares a module that is not installed. EXPECT: the run-time preflight fails BEFORE the
    body runs, with an Install-Module hint naming the missing module — NOT the script's own
    "term not recognized" error, and the marker line below must NOT run.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,module')]
[ScriptVersion('1.0.0')]
[ScriptIcon('📦')]
[ScriptOutput('Toast')]
[RequiresModule('PaletteShell.DefinitelyNotInstalled')]
[CmdletBinding()]
param()

Write-Host "BODY RAN — preflight did not block the missing module."

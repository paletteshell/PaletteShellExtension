using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Failure: Markdown mode (rich card)
.DESCRIPTION
    Fails on a display (Markdown) output mode, so the failure surfaces as the rich
    ScriptFailureForm adaptive card rendered on a page — NOT the ambient failure dialog.
    EXPECT: an adaptive card titled "…exited with code 3" with a stderr preview and a
    "View details" action, shown in place on the page.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,failure')]
[ScriptVersion('1.0.0')]
[ScriptIcon('❌')]
[ScriptOutput('Markdown')]
[CmdletBinding()]
param()

[Console]::Error.WriteLine("Simulated failure on stderr (Markdown mode).")
Write-Host "Some stdout before failing."
exit 3

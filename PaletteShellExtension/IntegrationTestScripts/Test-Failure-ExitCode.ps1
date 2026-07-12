using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Failure: non-zero exit
.DESCRIPTION
    Writes to stderr and exits 1. EXPECT: the run is surfaced as a FAILURE (not success),
    with a failure report showing the exit code and captured stderr.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,failure')]
[ScriptVersion('1.0.0')]
[ScriptIcon('❌')]
[ScriptOutput('Toast')]
[CmdletBinding()]
param()

[Console]::Error.WriteLine("Simulated failure on stderr.")
Write-Host "Some stdout before failing."
exit 1

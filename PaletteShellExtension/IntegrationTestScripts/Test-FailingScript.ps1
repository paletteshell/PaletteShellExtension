using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Test Failing Script
.DESCRIPTION
    Deliberately fails with a non-zero exit code, multi-line stderr, and some
    stdout — for verifying the failure dialog, the "View details" report, and
    the stderr line in the log.
#>
[ScriptGroup('Testing')]
[ScriptVersion('1.0.0')]
[ScriptIcon('💥')]
[ScriptOutput('Toast')]
param()

# Some stdout so the report's stdout section has content.
Write-Host 'Doing some pretend work...'
Write-Host 'Step 1 of 3 complete.'

# Multi-line stderr: exceeds nothing, but proves newline collapsing in the log
# line and full fidelity in the failure report.
Write-Error 'Something went wrong: could not frobnicate the widget.'
Write-Error 'Inner detail: widget service returned HTTP 503 (Service Unavailable).'
Write-Error "Hint: this is a test failure from Test-FailingScript.ps1 — everything is fine."

exit 1

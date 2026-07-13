using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: Toast
.DESCRIPTION
    EXPECT: the trimmed stdout is surfaced as the result/toast text and offered as a
    copy value. Confirms the default "surface captured output" path.
    NOTE: requires 'Toast' in KnownOutputModes (parser) — if it shows as a repair row,
    the parser's output-mode allow-list is missing 'Toast'.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🔔')]
[ScriptOutput('Toast')]
[CmdletBinding()]
param()

Write-Output "Toast OK @ $(Get-Date -Format o)"

using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: None
.DESCRIPTION
    Fire-and-forget run. EXPECT: script runs, palette reports "Script completed",
    and no output is surfaced (stdout is discarded). No clipboard/file/editor side effect.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🔇')]
[ScriptOutput('None')]
[CmdletBinding()]
param()

Write-Host "This line is written to stdout but should NOT be surfaced by None mode."

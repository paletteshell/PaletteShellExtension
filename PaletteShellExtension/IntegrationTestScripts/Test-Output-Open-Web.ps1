using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: Open (web, safe)
.DESCRIPTION
    EXPECT: the first stdout line is an https URL, treated as a SAFE target and opened in
    the default browser WITHOUT a confirmation prompt.
    NOTE: requires 'Open' in KnownOutputModes (parser). Repair row => allow-list missing 'Open'.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output,open')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🌐')]
[ScriptOutput('Open')]
[CmdletBinding()]
param()

Write-Output "https://example.com/"

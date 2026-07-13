using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: Clipboard
.DESCRIPTION
    EXPECT: full stdout is placed on the clipboard and palette reports "Copied to
    clipboard". Paste anywhere to verify the marker line below is on the clipboard.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output')]
[ScriptVersion('1.0.0')]
[ScriptIcon('📋')]
[ScriptOutput('Clipboard')]
[CmdletBinding()]
param()

Write-Output "CLIPBOARD-TEST marker $(New-Guid)"

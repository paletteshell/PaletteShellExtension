using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: Open (folder, safe)
.DESCRIPTION
    EXPECT: the first stdout line is an existing folder path, treated as SAFE and opened
    in File Explorer without a prompt.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output,open')]
[ScriptVersion('1.0.0')]
[ScriptIcon('📂')]
[ScriptOutput('Open')]
[CmdletBinding()]
param()

[System.IO.Path]::GetTempPath()

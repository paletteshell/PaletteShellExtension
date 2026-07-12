using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: File (json hint)
.DESCRIPTION
    EXPECT: stdout written to a temp file and opened in the editor. The 'File:json'
    extension hint should open it as a .json file (JSON syntax highlighting).
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🗂️')]
[ScriptOutput('File:json')]
param()

[pscustomobject]@{
    test      = 'File output with json extension hint'
    timestamp = (Get-Date -Format o)
    nested    = @{ ok = $true; items = @(1, 2, 3) }
} | ConvertTo-Json -Depth 5

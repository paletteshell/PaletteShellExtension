using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: Result
.DESCRIPTION
    EXPECT: stdout shown as a single copyable result (Enter copies; "Run again"
    regenerates a new value). Re-running must produce a different GUID.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🎯')]
[ScriptOutput('Result')]
param()

[System.Guid]::NewGuid().ToString()

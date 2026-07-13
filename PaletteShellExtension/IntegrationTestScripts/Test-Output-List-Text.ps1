using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: List (newline text)
.DESCRIPTION
    EXPECT: newline-delimited stdout parsed into a searchable list — one selectable
    item per line. Picking an item copies its text.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output,list')]
[ScriptVersion('1.0.0')]
[ScriptIcon('📃')]
[ScriptOutput('List')]
[CmdletBinding()]
param()

1..5 | ForEach-Object { "List item #$_" }

using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: List (JSON objects)
.DESCRIPTION
    EXPECT: a JSON array of objects parsed into list items, each showing a title and
    subtitle. Picking an item copies its 'value' (distinct from the title).
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output,list')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🗃️')]
[ScriptOutput('List')]
[CmdletBinding()]
param()

$items = 1..3 | ForEach-Object {
    [pscustomobject]@{
        title    = "Item $_"
        subtitle = "Subtitle for item $_ — picking copies value$_"
        value    = "copied-value-$_"
    }
}
$items | ConvertTo-Json -Depth 4 -Compress -AsArray | Write-Output

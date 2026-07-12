using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Output: List (live provider)
.DESCRIPTION
    EXPECT: used as a live provider — the single string parameter receives whatever you
    type in the search box, and the list refreshes as you type. Type text and confirm the
    echoed items update live.
.PARAMETER Query
    Live search text from the palette search box.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,output,list')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🔎')]
[ScriptOutput('List')]
[CmdletBinding()]
param(
    [Parameter()]
    [string]$Query
)

if ([string]::IsNullOrWhiteSpace($Query)) {
    @([pscustomobject]@{ title = 'Type to search…'; subtitle = 'Items echo your query live'; value = '' }) |
        ConvertTo-Json -Depth 4 -Compress -AsArray | Write-Output
    return
}

$items = 1..3 | ForEach-Object {
    [pscustomobject]@{
        title    = "$Query — result $_"
        subtitle = "Echoes the live query '$Query'"
        value    = "$Query-$_"
    }
}
$items | ConvertTo-Json -Depth 4 -Compress -AsArray | Write-Output

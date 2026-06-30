using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Pick a Git Branch
.DESCRIPTION
    List the local branches of a Git repository as a searchable, pickable list. Select a
    branch to copy its name. Demonstrates List output mode as a live provider.
.PARAMETER Path
    Folder inside the repository to inspect. In the palette, type or paste this path into
    the search box — the branch list refreshes as you type.
#>
[ScriptHost('pwsh')]
[ScriptGroup('Developer')]
[ScriptIcon('🌿')]
[ScriptTimeout(15000)]
[ScriptOutput('List')]
[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path
)

# List output works in two shapes: newline-delimited text (one item per line) or a JSON
# array. We emit a JSON array of objects so each item can carry a subtitle and a distinct
# copy value:
#   title    -> shown as the item's title (also used as the copy value if no 'value')
#   subtitle -> shown under the title
#   value    -> what gets copied when the item is picked
# Always emit a JSON array (even for the error case) so the picker has something to show.

function Write-ListResult {
    param([Parameter(Mandatory)][object[]]$Items)
    # Compress keeps it a single clean line of stdout for the parser.
    $Items | ConvertTo-Json -Depth 4 -Compress -AsArray | Write-Output
}

# When this script is used as a live provider, $Path is whatever the user has typed in the
# palette search box. Until they type something, prompt instead of guessing a folder.
if ([string]::IsNullOrWhiteSpace($Path)) {
    Write-ListResult @(
        [pscustomobject]@{
            title    = 'Type a repository folder path…'
            subtitle = 'Paste or type a path in the search box to list that repo''s branches.'
            value    = ''
        }
    )
    return
}

if (-not (Test-Path -LiteralPath $Path)) {
    Write-ListResult @(
        [pscustomobject]@{ title = 'Folder not found'; subtitle = $Path; value = $Path }
    )
    return
}

# Run git relative to the requested folder. -C makes git treat $Path as the working tree.
$inRepo = (& git -C $Path rev-parse --is-inside-work-tree 2>$null) -eq 'true'
if (-not $inRepo) {
    Write-ListResult @(
        [pscustomobject]@{
            title    = 'Not a Git repository'
            subtitle = "$Path is not inside a Git repo — type or paste a repo path in the search box."
            value    = $Path
        }
    )
    return
}

# One row per local branch. %(HEAD) is '*' for the checked-out branch; the format string
# packs name, last-commit subject and relative date so we can build a useful subtitle.
$branches = & git -C $Path for-each-ref --sort=-committerdate refs/heads `
    --format='%(HEAD)%09%(refname:short)%09%(contents:subject)%09%(committerdate:relative)'

$items = foreach ($line in $branches) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split "`t", 4
    $isCurrent = $parts[0].Trim() -eq '*'
    $name = $parts[1]
    $subject = if ($parts.Count -ge 3) { $parts[2] } else { '' }
    $when = if ($parts.Count -ge 4) { $parts[3] } else { '' }

    [pscustomobject]@{
        title    = if ($isCurrent) { "● $name" } else { $name }
        subtitle = (@($when, $subject) | Where-Object { $_ } ) -join '  —  '
        value    = $name
    }
}

if (-not $items) {
    Write-ListResult @(
        [pscustomobject]@{ title = 'No branches found'; subtitle = $Path; value = '' }
    )
    return
}

Write-ListResult @($items)

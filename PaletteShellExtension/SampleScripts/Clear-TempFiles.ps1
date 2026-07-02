using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Clean Temp Files
.DESCRIPTION
    Delete files in your user TEMP folder older than the given number of days, then report
    how many were removed and how much space was freed. Because it's destructive it asks for
    confirmation first — and since it takes a parameter, the prompt appears after you submit
    the form (demonstrates ConfirmBeforeRun on a parameterized script).
.PARAMETER OlderThanDays
    Only delete files last modified more than this many days ago.
#>
[ScriptHost('pwsh')]
[ScriptGroup('System')]
[ScriptIcon('🧹')]
[ScriptTimeout(60000)]
[ScriptOutput('Toast')]
[ConfirmBeforeRun('This permanently deletes old files from your TEMP folder.')]
param(
    [ValidateRange(0, 3650)]
    [int]$OlderThanDays = 7
)

$cutoff = (Get-Date).AddDays(-$OlderThanDays)
$files = Get-ChildItem -Path $env:TEMP -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt $cutoff }

$count = 0
$bytes = 0L
foreach ($file in $files) {
    try {
        $size = $file.Length
        Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
        $count++
        $bytes += $size
    }
    catch {
        # File is in use or protected; skip it.
    }
}

$mb = [math]::Round($bytes / 1MB, 1)
Write-Host "Deleted $count files · freed $mb MB"

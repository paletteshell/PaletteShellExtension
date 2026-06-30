using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Export Process List → CSV
.DESCRIPTION
    Snapshot running processes (name, PID, memory, CPU) as CSV and open it in the editor / Excel
#>
[ScriptHost('pwsh')]
[ScriptGroup('Utilities')]
[ScriptIcon('📊')]
[ScriptTimeout(15000)]
[ScriptOutput('File:csv')]
[CmdletBinding()]
param()

$rows = Get-Process |
    Sort-Object -Property WorkingSet64 -Descending |
    Select-Object `
        Name,
        Id,
        @{ Name = 'MemoryMB'; Expression = { [math]::Round($_.WorkingSet64 / 1MB, 1) } },
        @{ Name = 'CPUSeconds'; Expression = { if ($_.CPU) { [math]::Round($_.CPU, 1) } else { 0 } } },
        @{ Name = 'Threads'; Expression = { $_.Threads.Count } }

# Emit CSV to stdout; File output mode writes it to a temp .csv and opens it.
$rows | ConvertTo-Csv -NoTypeInformation | Write-Host

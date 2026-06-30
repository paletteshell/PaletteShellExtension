using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    System Report
.DESCRIPTION
    Show a quick system summary rendered as Markdown
#>
[ScriptHost('pwsh')]
[ScriptGroup('Utilities')]
[ScriptIcon('🖥️')]
[ScriptTimeout(15000)]
[ScriptOutput('Markdown')]
param()

$os = Get-CimInstance Win32_OperatingSystem
$cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name
$totalGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
$freeGB = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
$uptime = (Get-Date) - $os.LastBootUpTime

Write-Host "# System Report"
Write-Host ""
Write-Host "| Property | Value |"
Write-Host "| --- | --- |"
Write-Host "| Computer | $env:COMPUTERNAME |"
Write-Host "| User | $env:USERNAME |"
Write-Host "| OS | $($os.Caption) ($($os.Version)) |"
Write-Host "| CPU | $cpu |"
Write-Host "| Memory | $freeGB GB free / $totalGB GB total |"
Write-Host "| Uptime | $([math]::Floor($uptime.TotalHours))h $($uptime.Minutes)m |"
Write-Host "| PowerShell | $($PSVersionTable.PSVersion) |"

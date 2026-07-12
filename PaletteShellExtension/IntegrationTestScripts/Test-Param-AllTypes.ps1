using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Params: all types
.DESCRIPTION
    Exercises every parameter UI type the parser maps: string, int (ValidateRange),
    number/double, bool, switch, and enum (ValidateSet). Also covers Mandatory, a
    comment-help .PARAMETER label, an inline HelpMessage, and default values.
    EXPECT: the form renders the right control per type (dropdown for enum, toggle for
    switch/bool, numeric fields for int/number), enforces the range and the required
    field, and the Markdown table below echoes exactly what you submitted.
.PARAMETER Name
    A plain string (label comes from this comment-help line).
.PARAMETER Count
    Integer constrained to 1..10 via ValidateRange.
.PARAMETER Ratio
    A double/number value.
.PARAMETER Mode
    Enum rendered as a dropdown (ValidateSet).
.PARAMETER Enabled
    A [bool] — passed to PowerShell as $true/$false.
.PARAMETER Verbose2
    A [switch] — present when on, omitted when off.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,params')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🧩')]
[ScriptOutput('Markdown')]
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, HelpMessage="Required string — try leaving it blank")]
    [string]$Name = "world",

    [Parameter(HelpMessage="Integer 1..10")]
    [ValidateRange(1, 10)]
    [int]$Count = 3,

    [Parameter()]
    [double]$Ratio = 1.5,

    [Parameter(HelpMessage="Pick one")]
    [ValidateSet("Alpha", "Beta", "Gamma")]
    [string]$Mode = "Beta",

    [Parameter()]
    [bool]$Enabled = $true,

    [Parameter()]
    [switch]$Verbose2
)

Write-Host "# Parameter Binding Test"
Write-Host ""
Write-Host "| Parameter | Type | Value |"
Write-Host "| --- | --- | --- |"
Write-Host "| Name     | string | ``$Name`` |"
Write-Host "| Count    | int    | ``$Count`` |"
Write-Host "| Ratio    | double | ``$Ratio`` |"
Write-Host "| Mode     | enum   | ``$Mode`` |"
Write-Host "| Enabled  | bool   | ``$Enabled`` |"
Write-Host "| Verbose2 | switch | ``$($Verbose2.IsPresent)`` |"

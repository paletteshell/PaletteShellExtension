using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Transform Text
.DESCRIPTION
    Apply various transformations to clipboard text with customizable options
.PARAMETER Operation
    The transformation operation to perform
.PARAMETER Prefix
    Text to add before each line
.PARAMETER Suffix
    Text to add after each line
.PARAMETER RepeatCount
    Number of times to repeat each line
.PARAMETER RemoveEmptyLines
    Remove empty lines from the output
.EXAMPLE
    .\Text-Transform.ps1 -Operation "Quote" -Prefix '"' -Suffix '"'
    Wraps each line in double quotes
#>
[ScriptHost('pwsh')]
[ScriptGroup('Text Utilities')]
[ScriptIcon('🔄')]
[ScriptTimeout(15000)]
[ScriptOutput('None')]
[CmdletBinding()]
param(
    # Transformation type
    [Parameter(Mandatory=$true, HelpMessage="Select the transformation to apply")]
    [ValidateSet("Quote", "AddLineNumbers", "Indent", "Custom", "Repeat")]
    [string]$Operation = "Quote",

    # Prefix text (used in Custom mode)
    [Parameter(Mandatory=$false, HelpMessage="Text to add before each line")]
    [string]$Prefix = "",

    # Suffix text (used in Custom mode)
    [Parameter(Mandatory=$false, HelpMessage="Text to add after each line")]
    [string]$Suffix = "",

    # Number of spaces for indentation
    [Parameter(Mandatory=$false, HelpMessage="Number of spaces to indent")]
    [ValidateRange(1, 20)]
    [int]$IndentSpaces = 4,

    # Repeat count
    [Parameter(Mandatory=$false, HelpMessage="Times to repeat each line")]
    [ValidateRange(1, 100)]
    [int]$RepeatCount = 1,

    # Remove empty lines
    [Parameter(Mandatory=$false)]
    [switch]$RemoveEmptyLines
)

# Get clipboard text
$text = Get-ClipboardText

if ([string]::IsNullOrEmpty($text)) {
    Write-Host "⚠️ Clipboard is empty"
    exit 1
}

# Split into lines
$lines = $text -split "(\r\n|\n|\r)"
if ($RemoveEmptyLines) {
    $lines = $lines | Where-Object { $_ -ne "" -and $_ -notmatch '^\s*$' }
}

Write-Host "Processing $($lines.Count) lines with operation: $Operation"

# Apply transformation based on operation
$result = switch ($Operation) {
    "Quote" {
        $lines | ForEach-Object { '"' + ($_ -replace '"', '""') + '"' }
    }
    
    "AddLineNumbers" {
        $lineNum = 1
        $lines | ForEach-Object {
            if ($_ -match '\S') {  # Only number non-empty lines
                "$lineNum. $_"
                $lineNum++
            } else {
                $_
            }
        }
    }
    
    "Indent" {
        $indent = " " * $IndentSpaces
        $lines | ForEach-Object { $indent + $_ }
    }
    
    "Custom" {
        $lines | ForEach-Object { $Prefix + $_ + $Suffix }
    }
    
    "Repeat" {
        $lines | ForEach-Object {
            $line = $_
            1..$RepeatCount | ForEach-Object { $line }
        }
    }
}

# Join lines back together
$output = $result -join "`n"

# Copy to clipboard
Set-ClipboardText $output

# Report results
Write-Host "✅ Transformation complete!"
Write-Host "   Operation: $Operation"
if ($Operation -eq "Indent") {
    Write-Host "   Indent: $IndentSpaces spaces"
}
if ($Operation -eq "Custom") {
    Write-Host "   Prefix: '$Prefix'"
    Write-Host "   Suffix: '$Suffix'"
}
if ($Operation -eq "Repeat") {
    Write-Host "   Repeat count: $RepeatCount"
}
Write-Host "   Lines processed: $($lines.Count)"
Write-Host "   Output copied to clipboard"

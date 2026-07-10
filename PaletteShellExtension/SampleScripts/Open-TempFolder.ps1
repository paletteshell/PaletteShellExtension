using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Open Temp Folder
.DESCRIPTION
    Open the current user's temp folder in File Explorer. Demonstrates Open output mode.
#>
[ScriptHost('pwsh')]
[ScriptGroup('Utilities')]
[ScriptVersion('1.0.0')]
[ScriptIcon('📂')]
[ScriptTimeout(5000)]
[ScriptOutput('Open')]
[RequiresPaletteShellMinimum('0.0.7')]
[CmdletBinding()]
param()

# Open output uses the first non-empty stdout line as a URL, file, or folder path.
[System.IO.Path]::GetTempPath()

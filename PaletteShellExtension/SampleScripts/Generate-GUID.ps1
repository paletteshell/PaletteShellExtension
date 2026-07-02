using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Generate GUID
.DESCRIPTION
    Generate a new GUID and show it as a result — press Enter to copy it,
    or "Run again" to generate a fresh one.
#>
[ScriptHost('pwsh')]
[ScriptGroup('Utilities')]
[ScriptIcon('🆔')]
[ScriptTimeout(5000)]
[ScriptOutput('Result')]
param()

# Emit just the value on stdout; Result mode shows it as a copyable result.
[System.Guid]::NewGuid().ToString()

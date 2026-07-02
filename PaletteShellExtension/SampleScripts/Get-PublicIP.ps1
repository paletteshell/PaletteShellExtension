using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Get Public IP
.DESCRIPTION
    Look up this machine's public IP address and show it as a result —
    press Enter to copy it, or "Run again" to re-check.
#>
[ScriptHost('pwsh')]
[ScriptGroup('Utilities')]
[ScriptIcon('🌐')]
[ScriptTimeout(10000)]
[ScriptOutput('Result')]
param()

# Emit just the address on stdout; Result mode shows it as a copyable result.
# A longer timeout is set above since this depends on a network round-trip.
(Invoke-RestMethod -Uri 'https://api.ipify.org?format=text').Trim()

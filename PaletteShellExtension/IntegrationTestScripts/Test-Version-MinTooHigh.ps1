using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Attr: RequiresPaletteShellMinimum (too high)
.DESCRIPTION
    Requires an unrealistically high minimum PaletteShell version. EXPECT: the script is
    hidden from the runnable list and shown as an explanatory row saying the installed app
    is too old — it must NOT be runnable.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,version')]
[ScriptVersion('1.0.0')]
[ScriptIcon('⬆️')]
[ScriptOutput('Toast')]
[RequiresPaletteShellMinimum('99.0.0')]
[CmdletBinding()]
param()

Write-Host "If you can run this, the minimum-version gate is not enforced."

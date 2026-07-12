using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Attr: RequiresPaletteShellMaximum (too low)
.DESCRIPTION
    Sets a maximum PaletteShell version below the current app. EXPECT: the script is hidden
    from the runnable list and shown as an explanatory row saying the installed app is too
    new — it must NOT be runnable.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,version')]
[ScriptVersion('1.0.0')]
[ScriptIcon('⬇️')]
[ScriptOutput('Toast')]
[RequiresPaletteShellMaximum('0.0.1')]
[CmdletBinding()]
param()

Write-Host "If you can run this, the maximum-version gate is not enforced."

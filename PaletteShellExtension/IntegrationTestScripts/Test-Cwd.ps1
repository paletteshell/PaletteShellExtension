using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Attr: ScriptCwd (token)
.DESCRIPTION
    Sets [ScriptCwd('{Temp}')] using a path token. EXPECT: the process working directory is
    the user's TEMP folder — the reported PWD below matches %TEMP%.
    Supported tokens: {ScriptDir}, {Home}, {Temp}.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,cwd')]
[ScriptVersion('1.0.0')]
[ScriptIcon('📁')]
[ScriptOutput('Markdown')]
[ScriptCwd('{Temp}')]
[CmdletBinding()]
param()

Write-Host "# ScriptCwd Test"
Write-Host ""
Write-Host "- Working directory (PWD): ``$($PWD.Path)``"
Write-Host "- Expected (\$env:TEMP):   ``$env:TEMP``"

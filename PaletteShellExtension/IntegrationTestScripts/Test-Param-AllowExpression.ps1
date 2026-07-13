using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Params: AllowExpression
.DESCRIPTION
    The Expr parameter is marked [AllowExpression], so the form value is injected verbatim
    for PowerShell to evaluate instead of being quoted as a literal string.
    EXPECT: enter e.g. (2 + 3) * 4 and the result is 20. A normal [string] param would show
    the literal text "(2 + 3) * 4" instead.
    Compare with Literal, which is quoted and echoed as-is.
.PARAMETER Expr
    A PowerShell expression, evaluated (AllowExpression).
.PARAMETER Literal
    A literal string, quoted as-is.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,params')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🧮')]
[ScriptOutput('Markdown')]
[CmdletBinding()]
param(
    [Parameter(HelpMessage="A PowerShell expression, e.g. (2 + 3) * 4")]
    [AllowExpression()]
    $Expr = 42,

    [Parameter(HelpMessage="A literal string")]
    [string]$Literal = "(2 + 3) * 4"
)

Write-Host "# AllowExpression Test"
Write-Host ""
Write-Host "- **Expr** (evaluated): ``$Expr``"
Write-Host "- **Literal** (quoted): ``$Literal``"

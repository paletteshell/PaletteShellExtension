using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Failure: secret redaction
.DESCRIPTION
    Takes secret-looking params (Password/Token/ApiKey) and then fails (exit 1) so the
    failure report includes the Args line. EXPECT: in the failure report the secret VALUES
    are masked as '***' while the switch names remain — e.g. -Token '***'. The non-secret
    -User value must NOT be masked.
.PARAMETER User
    A non-secret value (should appear in the clear).
.PARAMETER Password
    Secret — value must be redacted in the failure report.
.PARAMETER Token
    Secret — value must be redacted.
.PARAMETER ApiKey
    Secret — value must be redacted.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,failure,security')]
[ScriptVersion('1.0.0')]
[ScriptIcon('🔒')]
[ScriptOutput('Toast')]
[CmdletBinding()]
param(
    [Parameter()]
    [string]$User = "alice",

    [Parameter()]
    [string]$Password = "hunter2",

    [Parameter()]
    [string]$Token = "tok_live_should_be_hidden",

    [Parameter()]
    [string]$ApiKey = "ak_should_be_hidden"
)

Write-Host "Deliberately failing so the redacted Args line is shown."
exit 1

using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    [IT] Confirm: after form submit
.DESCRIPTION
    Because this script has a parameter, the confirmation prompt must appear AFTER you
    submit the form (not before it). EXPECT: fill the form, submit, THEN see the confirm
    dialog. Cancel => nothing runs.
.PARAMETER Label
    Any text; echoed back on confirm.
#>
[ScriptHost('auto')]
[ScriptGroup('Integration Tests')]
[ScriptTags('integration,confirm')]
[ScriptVersion('1.0.0')]
[ScriptIcon('⚠️')]
[ScriptOutput('Toast')]
[ConfirmBeforeRun('Integration test: confirm after submitting the form.')]
param(
    [Parameter(HelpMessage="Any label to echo")]
    [string]$Label = "confirmed"
)

Write-Output "Ran with Label='$Label'"

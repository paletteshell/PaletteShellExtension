# PaletteShell Integration Test Scripts

A manual, feature-by-feature regression suite. Each script exercises **one** piece of
PaletteShell functionality and its `.DESCRIPTION` states the **EXPECT**ed behavior when run
through Command Palette. Run the suite before a release to confirm every surface still works
end-to-end (parse → form → execute → route output).

These are **not** automated unit tests — the parser has those in
`PaletteShellExtension.Tests/PowerShellScriptParserTests.cs`. These verify the real runtime
path through the palette that unit tests can't reach (UAC, editors, clipboard, live List
providers, timeouts, etc.).

## How to run

1. Copy every `Test-*.ps1` in this folder into your PaletteShell **scripts directory** — the
   same folder where `PaletteScriptAttributes.psm1` is deployed (the scripts reference it via
   `using module .\PaletteScriptAttributes.psm1`, and the `[Script*]` attributes only resolve
   at runtime when that module sits beside them). Copy `PaletteScriptAttributes.psm1` in too if
   it isn't already there.
2. Open Command Palette → the scripts appear under the **Integration Tests** group.
3. For each, run it and compare the actual behavior against the **EXPECT** line in its
   description. They are safe and non-destructive (the only writes are marker files/clipboard).

## Coverage matrix

| Feature | Script | Expected |
| --- | --- | --- |
| `ScriptOutput('None')` | `Test-Output-None.ps1` | Runs, "Script completed", stdout discarded |
| `ScriptOutput('Toast')` | `Test-Output-Toast.ps1` | stdout surfaced as toast/result |
| `ScriptOutput('Clipboard')` | `Test-Output-Clipboard.ps1` | stdout copied to clipboard |
| `ScriptOutput('Result')` | `Test-Output-Result.ps1` | Single copyable result; "Run again" regenerates |
| `ScriptOutput('Markdown')` | `Test-Output-Markdown.ps1` | stdout rendered as Markdown |
| `ScriptOutput('File:json')` | `Test-Output-File-Json.ps1` | Temp file opened in editor as `.json` |
| `ScriptOutput('List')` text | `Test-Output-List-Text.ps1` | Newline lines → searchable items |
| `ScriptOutput('List')` JSON | `Test-Output-List-Json.ps1` | JSON objects → items w/ title/subtitle/value |
| `List` live provider | `Test-Output-List-Live.ps1` | Query param = live search text; list refreshes as you type |
| `ScriptOutput('Open')` web | `Test-Output-Open-Web.ps1` | https URL opened, no prompt (safe target) |
| `ScriptOutput('Open')` folder | `Test-Output-Open-Folder.ps1` | Folder opened in Explorer, no prompt |
| Param types (string/int/number/bool/switch/enum), Mandatory, HelpMessage, ValidateRange, ValidateSet, defaults | `Test-Param-AllTypes.ps1` | Correct control per type; echoed values match input |
| `[AllowExpression()]` | `Test-Param-AllowExpression.ps1` | Expr evaluated; literal string quoted as-is |
| `ConfirmBeforeRun` (no params) | `Test-Confirm-NoParams.ps1` | Confirm dialog before run; cancel = no run |
| `ConfirmBeforeRun` (+ params) | `Test-Confirm-WithParams.ps1` | Confirm appears AFTER form submit |
| `RequiresElevation` | `Test-Elevation.ps1` | UAC prompt; elevated run (marker file in %TEMP%) |
| `ScriptTimeout` enforcement | `Test-Timeout.ps1` | Killed at ~2s; surfaced as timeout failure |
| Non-zero exit / stderr | `Test-Failure-ExitCode.ps1` | Surfaced as failure with exit code + stderr |
| Secret arg redaction | `Test-Failure-Redaction.ps1` | Password/Token/ApiKey values masked `***` in report; User in clear |
| `ScriptEnv` | `Test-Env.ps1` | Declared env vars present with declared values |
| `ScriptCwd` + `{Temp}` token | `Test-Cwd.ps1` | Working dir = %TEMP% |
| `ScriptHost('powershell')` | `Test-Host-WindowsPowerShell.ps1` | Runs under powershell.exe (5.x / Desktop) |
| `ScriptHost('pwsh')` | `Test-Host-Pwsh.ps1` | Runs under pwsh.exe (7.x / Core), or clear "pwsh required" error |
| `RequiresModule` (missing) | `Test-RequiresModule-Missing.ps1` | Preflight fails with Install-Module hint; body never runs |
| `RequiresPaletteShellMinimum` | `Test-Version-MinTooHigh.ps1` | Hidden/explanatory row; not runnable |
| `RequiresPaletteShellMaximum` | `Test-Version-MaxTooLow.ps1` | Hidden/explanatory row; not runnable |
| Fail-closed parsing | `Test-Parse-FailClosed.ps1` | Disabled "repair" row (bad ScriptTimeout); not runnable |

## Also visible on every script

`ScriptGroup('Integration Tests')`, `ScriptTags(...)`, `ScriptVersion`, and `ScriptIcon` are
set on all scripts — confirm the group, per-script emoji icon, and tags render correctly in the
palette while you go through the list.

## Keeping this suite current

**When you change a feature, update the matching script here in the same change** (and this
matrix). Specifically:

- **New `[Script*]` attribute or param feature** → add a `Test-*.ps1` covering it + a matrix row.
- **New/renamed `ScriptOutput` mode** → add/rename the `Test-Output-*.ps1` and update
  `KnownOutputModes` expectations. (The parser's allow-list must include every mode the
  dispatcher handles — currently `None, Toast, Clipboard, File, Markdown, Result, List, Open`.)
- **Changed redaction rules** (`ScriptFailureReport.RedactArgs`) → update
  `Test-Failure-Redaction.ps1` param names.
- **Changed path tokens** (`ExpandPathTokens`: `{ScriptDir} {Home} {Temp}`) → update `Test-Cwd.ps1`.
- **Changed timeout bounds** (`MinTimeoutMs`/`MaxTimeoutMs`) → keep `Test-Timeout.ps1` within them.

Naming: `Test-<Area>-<Case>.ps1`, group `Integration Tests`, tag with `integration`, and always
put the pass/fail criterion on an **EXPECT** line in `.DESCRIPTION`.

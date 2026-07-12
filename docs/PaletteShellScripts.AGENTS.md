# PaletteShell Script Agent Guide

This folder contains PaletteShell `.ps1` scripts. Keep this file short so agents can start cheaply.
For the full contract, read `PaletteShellScripts.Reference.md`.

## Required Script Shape

```powershell
using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Short command title
.DESCRIPTION
    Longer subtitle
.PARAMETER Name
    Form label or placeholder
#>
[ScriptHost('pwsh')]
[ScriptIcon('')]
[ScriptOutput('Clipboard')]
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Name
)

# body
```

## Hard Rules

- Put `using module .\PaletteScriptAttributes.psm1` on line 1.
- Put every `[Script*]` attribute above `param(...)`.
- Use a `<# ... #>` help block with `.SYNOPSIS`, `.DESCRIPTION`, and `.PARAMETER` entries.
- Save as UTF-8 with BOM when the script contains emoji or may run under Windows PowerShell 5.1.
- Emit captured results on stdout and exit non-zero on failure.
- Scripts live at the top level of the scripts folder; subfolders are not scanned.
- Changes appear after the user runs `Reload scripts`.

## Common Attributes

- `[ScriptHost('pwsh')]` or `[ScriptHost('powershell')]`
- `[ScriptOutput('None'|'Toast'|'Clipboard'|'Markdown'|'Result'|'List'|'Open'|'File[:ext]')]`
- `[ScriptIcon('...')]`
- `[ScriptTimeout(30000)]`
- `[ScriptCwd('{ScriptDir}')]`
- `[ScriptEnv('NAME', 'value')]`
- `[RequiresModule('ModuleName')]`
- `[RequiresElevation()]`
- `[ConfirmBeforeRun('message')]`
- `[ScriptGroup('Category')]`
- `[ScriptTags('foo,bar')]`
- `[RequiresPaletteShellMinimum('1.2.0')]`
- `[RequiresPaletteShellMaximum('2.0.0')]`

## Output Mode Choice

- `None`: quick side effect, no captured output.
- `Toast`: short captured status.
- `Clipboard`: transform or generate text for the clipboard.
- `Markdown`: readable report page.
- `Result`: one copyable value, such as GUID/password/token.
- `List`: searchable pick list; one parameter becomes the live search query.
- `Open`: first non-empty stdout line is a URL, file, or folder to open.
- `File:csv` / `File:json` / `File`: large or structured output.

## Input Design

PaletteShell scripts start cold: no current directory, current file, selection, project, or argv.
Prefer low-friction context in this order:

1. No input: clipboard or fixed/token paths.
2. Optional field with a sensible default.
3. `[ValidateSet(...)]` dropdown.
4. One well-labeled required field.
5. `List` live provider for search/pick/filter workflows.

Avoid scripts that require several pasted paths or lots of form fields; those usually belong in a terminal or app.

## Before Finishing

- Check whether `PaletteShellScripts.Reference.md` is needed for exact syntax.
- Keep input friction proportional to payoff.
- Use `[ConfirmBeforeRun(...)]` for destructive actions.
- Use `[RequiresElevation()]` only with `[ScriptOutput('None')]`.
- Keep waited output modes under the timeout.

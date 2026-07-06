# Authoring PaletteShell scripts — guide for AI agents

This file is the **contract** for writing `.ps1` scripts that PaletteShell will load and run.
Follow it exactly. It is written for AI coding agents (and humans) who scaffold or edit scripts
in a PaletteShell scripts folder.

> A copy of this file ships into every user's `Documents\PaletteShellScripts` folder next to
> `PaletteScriptAttributes.psm1`. If you are an agent working in that folder, treat this as the
> authoritative spec — do not guess at attribute names or output modes.

## What PaletteShell does with a script

PaletteShell reads **metadata out of each `.ps1` file without executing it**, using a lightweight
text parser (`PowerShellScriptParser`). It then either runs the script or renders a form/page from
that metadata. Because the parser is text-based, **your script must follow the regular shape below**
— metadata placed in the wrong spot is silently ignored.

Scripts live in `Documents\PaletteShellScripts` (top level only — subfolders are not scanned).
Changes are picked up when the user runs **"Reload scripts"**, not automatically.

## The required shape

```powershell
using module .\PaletteScriptAttributes.psm1   # line 1 — lets the [Script*] attributes resolve at runtime

<#
.SYNOPSIS
    Short title (this becomes the command's title in the palette)
.DESCRIPTION
    Longer description (becomes the subtitle)
.PARAMETER MyParameter
    Help text for MyParameter (becomes its form-field label / placeholder)
#>
[ScriptHost('pwsh')]          # <-- all script-level attributes go ABOVE param(...)
[ScriptGroup('My Category')]
[ScriptIcon('🎯')]
[ScriptOutput('Clipboard')]
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$MyParameter
)

# --- script body ---
$result = $MyParameter.ToUpper()
Set-ClipboardText $result
```

**Hard rules the parser enforces:**

1. `using module .\PaletteScriptAttributes.psm1` should be the **first line**. It is required for the
   custom `[Script*]` attributes and the helper functions to resolve when the script actually runs.
2. All `[Script*]` attributes must appear **before the `param(` keyword**. Attributes after `param(`
   or inside comments are ignored.
3. Comment-based help must be a `<# ... #>` block. Only `.SYNOPSIS`, `.DESCRIPTION`, and
   `.PARAMETER <Name>` are read (`.EXAMPLE`, `.NOTES`, etc. are fine to include but ignored by the UI).
4. If there is no `.SYNOPSIS`, the title falls back to the file name. Always provide one.
5. Save the file as **UTF-8** (emoji icons and non-ASCII rely on it).

## Script-level attributes

Defined in `PaletteScriptAttributes.psm1`. Only these are recognized; anything else is ignored.

| Attribute | Purpose |
|-----------|---------|
| `[ScriptHost('pwsh')]` | Host to run under: `'pwsh'` (PowerShell 7, default) or `'powershell'` (Windows PowerShell 5.1) |
| `[ScriptCwd('{ScriptDir}')]` | Working directory (supports path tokens, below) |
| `[ScriptGroup('Category')]` | Group name used by tooling such as the Script Manager catalog browser |
| `[ScriptTags('foo,bar,baz')]` | Comma-delimited free-form tags used by tooling such as the Script Manager catalog browser |
| `[RequiresPaletteShellMinimum('1.2.0')]` | Minimum PaletteShell app version required. If the installed app is older, the row shows "Requires an update" instead of running the script. Defaults to `0.0.6` (the last release before this attribute existed) when omitted |
| `[RequiresPaletteShellMaximum('2.0.0')]` | Maximum PaletteShell app version this script still works on. If the installed app is newer, the row shows "Requires an update" instead of running the script. Optional — use only if your script depends on behavior later removed or changed |
| `[ScriptIcon('🚀')]` | Emoji or glyph shown on the row |
| `[ScriptOutput('None')]` | How stdout is handled (see [Output modes](#output-modes)) |
| `[ScriptTimeout(30000)]` | Timeout in **milliseconds**; also forces wait-and-capture |
| `[ScriptEnv('VAR', 'value')]` | Set an environment variable; repeat the attribute for more than one |
| `[RequiresElevation()]` | Run elevated (admin). Equivalent to `#Requires -RunAsAdministrator`. Output capture is unavailable when elevated. |
| `[ConfirmBeforeRun('message')]` | Show a yes/no dialog with `message` before running. Pair with `[RequiresElevation()]` for destructive scripts. |

### Path tokens

`[ScriptCwd(...)]` and `[ScriptEnv(...)]` values expand these at runtime:

- `{ScriptDir}` — the folder containing the script
- `{Home}` — the user's profile folder
- `{Temp}` — the system temp folder

## Output modes

Set with `[ScriptOutput('<mode>')]`. Default is `None`.

| Mode | Behavior |
|------|----------|
| `None` | Run silently; a "Script completed" toast is shown. With no `[ScriptTimeout]` this is **fire-and-forget** (output is not captured). |
| `Toast` | Wait, capture stdout, show it in a Windows notification. |
| `Clipboard` | Wait, capture stdout, copy it to the clipboard. |
| `Markdown` | Wait, render stdout as formatted Markdown on its own page. |
| `Result` | Wait, show stdout as a single copyable result (Enter copies; a **Run again** command regenerates). Print just the value; good for generators (GUID, password, token). |
| `File` | Write stdout to a temp file and open it in the user's editor. Add an extension hint after a colon: `File:csv`, `File:json`, etc. Best for large/structured output. |
| `List` | Parse stdout into a searchable, pickable list — turns the script into a search/pick provider (see below). |
| `Open` | Open the first non-empty stdout line as a URL, file, or folder path. |

Anything other than `None` (or any `[ScriptTimeout]`) makes PaletteShell **wait** for the process,
up to the timeout (30s default) before killing it. So: emit results on **stdout** (`Write-Host` /
`Write-Output` are captured), keep the run under the timeout, and exit non-zero on failure.

## Parameters → form fields

If a script declares a `param(...)` block (and is not a `List` script), PaletteShell auto-generates
an input form. Type and validation map to UI:

| PowerShell | Form field |
|------------|------------|
| `[string]` | text box |
| `[int]` / `[long]` | integer input |
| `[double]` / `[decimal]` / `[single]` | number input |
| `[switch]` / `[bool]` | checkbox |
| `[ValidateSet('A','B','C')]` | dropdown |
| `[ValidateRange(min,max)]` | min/max bounds on the number input |
| `[Parameter(Mandatory=$true)]` | required field |

The field **label** comes from `[Parameter(HelpMessage='...')]`, else the `.PARAMETER` help, else the
variable name. Provide one of the first two so the form reads well.

By default a form value is passed to the script as a **literal string**. If a parameter should accept
a PowerShell expression instead (evaluated, not quoted), mark it `[AllowExpression()]`.

## Designing inputs — what a PaletteShell script can (and can't) assume

This is the most important design decision, and the one agents get wrong most often. **A PaletteShell
script starts cold.** Unlike a shell command or an editor extension, it has **no ambient context**:

- **No current working directory** — the palette has no concept of "where you are." `[ScriptCwd(...)]`
  only sets a *fixed* or token-based directory (`{ScriptDir}`, `{Home}`, `{Temp}`), never "the folder
  I'm looking at." A script cannot discover a directory the way `cd`-relative shell tools do.
- **No current file, selection, or project** — there is nothing selected to operate on.
- **No prior command / no argv** beyond what you collect explicitly.

So every piece of context a script needs must arrive through one of these channels, each with a cost:

| Channel | How | Friction | Best for |
|---------|-----|----------|----------|
| **Fixed / token path** | `[ScriptCwd]`, `[ScriptEnv]`, `{Home}`/`{Temp}`, or a constant in the body | None — zero input | A target that is always the same (temp cleanup, a known repo, "my downloads") |
| **Clipboard** | `Get-ClipboardText` (transform, `Set-ClipboardText` back) | Low *if* the value is already copied; cumbersome if the user must go copy it first | Text/paths the user just yanked from elsewhere — the core clipboard-utility pattern |
| **Form field** | a `param(...)` entry → auto form | Medium — the user types/pastes each run | Occasional values, or a required value with no sensible default |
| **Live List provider** | one-parameter `[ScriptOutput('List')]`; search box *is* the input | Low — type-to-refine, no form, no submit | Search/pick/filter over a space (branches, files, lookups) |

### The working-directory problem, concretely

You **cannot** write a script that "runs in the current folder" — there is no current folder. To act on
a directory you must **make the directory an input**:

- A **form field** (`[string]$Path`) works but means pasting a long path on every run — heavy for a
  keyboard-driven launcher.
- The **clipboard** works if the user already copied the path, but forcing them to go copy it first, run
  the script, and read the result is more steps than just opening a terminal.
- A **live List provider** (path as the query parameter, results refresh as you type) is usually the
  best fit — see `Git-Branches.ps1`, which takes a repo path as its live query.

### Aim for the happy medium

Match the **input cost to the payoff**. A one-keystroke utility should not demand a paste; a script that
needs rich context should earn it. Prefer, in order:

1. **No input** — operate on the clipboard or a fixed/derivable location. This is the sweet spot for a
   launcher: select → done.
2. **A default that makes the field optional** — e.g. default `$Path` to `{Home}` or a sensible root, so
   the form can be submitted empty for the common case and overridden only when needed.
3. **A dropdown over free text** — `[ValidateSet(...)]` turns "type the exact value" into a quick pick,
   removing typos and typing.
4. **A single meaningful field**, well-labeled via `.PARAMETER` / `HelpMessage`.

**Avoid** designing a script that requires several heavy inputs (two paths, a path *and* a query, …): the
collection friction usually exceeds what the palette is good for, and that task belongs in a terminal or a
real app. If a script only makes sense with a lot of context, that is a signal it is the wrong tool for the
palette.

## List output — static list vs. live provider

`[ScriptOutput('List')]` runs the script and turns its stdout into a searchable list. Picking an item
copies its value; items with a `url` also get an **Open** command. The `param()` block decides the mode:

- **No parameter** → the script runs once; the palette search box filters the results locally.
- **One parameter** → **live provider**: the palette's search text is passed as that parameter and the
  list refreshes as the user types. Only the first parameter is used; the parameter form is skipped.
  The script also runs once on open with an empty value — handle the blank case (return a
  "Type a path…" prompt item rather than erroring).

Stdout is parsed as either:

- **Newline-delimited text** — each non-empty line is an item (title = copy value).
- **A JSON array** (e.g. `... | ConvertTo-Json -AsArray -Compress`). Array of strings behaves like the
  line case; array of objects maps these fields (all optional, case-insensitive):

  | Field | Purpose |
  |-------|---------|
  | `title` / `name` / `label` / `text` | Item title (and copy value if `value` omitted) |
  | `subtitle` / `description` / `detail` | Secondary line |
  | `value` / `copy` | Text copied when the item is picked |
  | `url` / `link` | Adds an **Open** command that launches the URL |
  | `icon` | Emoji or glyph on the item |

```powershell
# Live provider example
<#
.PARAMETER Query
    Type a search term…
#>
[ScriptOutput('List')]
param([string]$Query)

@(
    [pscustomobject]@{ title = "Result for $Query"; subtitle = 'picked → copied'; value = $Query }
) | ConvertTo-Json -AsArray -Compress
```

## Open output

`[ScriptOutput('Open')]` runs the script and opens the first non-empty stdout line as a URL,
file, or folder path. Emit only the target on stdout.

```powershell
[ScriptOutput('Open')]
param()

[System.IO.Path]::GetTempPath()
```

## Helper functions (from the module)

Because line 1 imports `PaletteScriptAttributes.psm1`, these are available:

```powershell
$text = Get-ClipboardText          # read clipboard (cross-platform, with fallbacks)
Set-ClipboardText "Hello World"    # write clipboard (cross-platform)
```

Most clipboard-transform scripts follow: read with `Get-ClipboardText`, guard the empty case,
transform, write back with `Set-ClipboardText`, and `Write-Host` a short status line.

## Checklist before you finish a script

- [ ] `using module .\PaletteScriptAttributes.psm1` on line 1.
- [ ] `<# .SYNOPSIS ... #>` block with a real title and description; a `.PARAMETER` line per parameter.
- [ ] All `[Script*]` attributes are above `param(`, and only use the recognized attribute names above.
- [ ] Input cost fits the payoff: no ambient working dir/selection exists, so prefer clipboard or a fixed/token path over forcing a pasted path; give optional fields a sensible default; avoid multiple heavy inputs.
- [ ] Output mode matches how results are surfaced; results go to **stdout**.
- [ ] Destructive or admin scripts have `[ConfirmBeforeRun('...')]` (and `[RequiresElevation()]` if needed).
- [ ] Long-running scripts set a realistic `[ScriptTimeout(ms)]`.
- [ ] Saved as UTF-8; icon is a single emoji/glyph.
- [ ] Exits non-zero on failure so the palette reports it.

See the bundled sample scripts (`Text-Transform.ps1`, `Git-Branches.ps1`, `Open-TempFolder.ps1`,
`System-Report.ps1`, `Clear-TempFiles.ps1`, …) for working examples of each pattern, and the community library at
<https://github.com/paletteshell/PaletteShellScripts>.

# Extension Behavior Reference

## How It Works

### Discovery

When the extension is activated, `PaletteShellExtensionPage` does the following:

1. Resolves the scripts folder: the one saved in [Settings](#settings), or — for an install that predates that setting — the existing `Documents\PaletteShellScripts` if it's already there. If neither exists yet, the page shows a **"Choose scripts folder"** prompt (suggesting `Documents\PaletteShellScripts`) instead of a script list, and the rest of this flow runs once you submit it.
2. Creates that directory if it doesn't exist.
3. Copies the embedded sample scripts (only files that aren't already there, so your edits are never overwritten).
4. Copies the `PaletteScriptAttributes.psm1` module, `AGENTS.md` authoring guide, and `PaletteShellScripts.Reference.md` next to the scripts so they're available at runtime.
5. Enumerates every `*.ps1` file in the folder (top level only) and builds the command list.

The list always begins with these built-in actions:

- **Open scripts folder** — opens your configured scripts folder in Explorer.
- **Open log folder** — opens the diagnostic log folder for script runs and failures.
- **Reload scripts** — re-scans the folder so new or changed scripts appear.
- **Create new script** — opens a guided wizard that scaffolds a new `.ps1` with metadata headers.
- **Browse community scripts** — opens the Script Manager, or falls back to the community [PaletteShellScripts](https://github.com/paletteshell/PaletteShellScripts) repository if it isn't installed.

Every script item carries a context menu (right-click, or the ⋯ commands) with:

- **Pin to top / Unpin** — pins the script so it always sorts above the rest (see [Pinning](#pinning)).
- **Open in editor** — opens the source file in your preferred editor.
- **Reveal in File Explorer** — opens Explorer with the script file selected, for managing it directly.
- **Delete script** — sends the script to the Recycle Bin after a confirmation dialog, then reloads the list.

Scripts are ordered **pinned first, then alphabetically by their displayed title** (the `.SYNOPSIS`, which often differs from the file name). Scripts without a `[ScriptIcon]` get a default terminal glyph so every row is scannable.

If a script fails to parse (e.g. a malformed `param()` block), it isn't dropped silently — it still appears with a **⚠ Couldn't load this script** subtitle and its context menu, so you can open it to fix or delete it.

A script that declares `[RequiresPaletteShellMinimum(...)]` / `[RequiresPaletteShellMaximum(...)]` outside the installed app's version range also stays visible, but shows a **⚠ Requires an update** row (naming the version it needs) instead of running, so you know to update PaletteShell rather than seeing a broken script.

A script that declares no `[ScriptVersion(...)]` is **treated as `1.0.0`** when loaded — the file itself is never rewritten. Scripts scaffolded through the **"Create new script"** wizard get a `[ScriptVersion('1.0.0')]` written in up front, so every managed script carries a version tools can compare.

> ℹ️ New scripts and edits are picked up only when you run **"Reload scripts"** — this is intentional, not a bug. Reloading shows a **"Reloaded N scripts"** toast so you know the rescan ran.

### Parsing the manifest

For each script, `PowerShellScriptParser` parses the file with a lightweight text parser — it never executes the script just to read metadata. From the script text it extracts:

- **Title** from the comment-based help `.SYNOPSIS` (falls back to the file name).
- **Description** from `.DESCRIPTION`.
- **Parameters** from the `param()` block, including type, default value, whether it's mandatory, and validation info (`[ValidateSet(...)]` becomes a dropdown, `[ValidateRange(...)]` becomes min/max bounds).
- **Behavior attributes** such as host, working directory, timeout, output mode, icon, environment variables, elevation, and confirmation (see the [attribute reference](#available-attributes)).
- **Elevation** from either the `[RequiresElevation()]` attribute or the built-in `#Requires -RunAsAdministrator` directive.
- **Confirmation** from the `[ConfirmBeforeRun('message')]` attribute, which gates the run behind a yes/no dialog.

### Running a script

Selecting a script item routes to one of these paths, based on its metadata:

- **Has parameters** → opens `ScriptParameterFormPage`, an auto-generated form. Once you submit, the collected values are passed to the script. `List` scripts are the exception: their first parameter is treated as the search query for live-provider behavior.
- **No parameters, a display mode** (`[ScriptOutput('Markdown')]` / `'Result'` / `'List'`) → opens a page that **stays open** and renders the output: `ScriptMarkdownPage` (formatted Markdown), `ScriptResultPage` (a single copyable result — see [Result output](#result-output)), or `ScriptListPage` (a searchable list — see [List output](#list-output)).
- **No parameters, an ambient mode** (`None` / `Toast` / `Clipboard` / `Open` / `File`) → runs the script, performs its side effect, shows a toast, and **dismisses the palette** — fire-and-forget. `RunScriptCommand` handles the pure `None` + no-timeout case (nothing to wait for); `AmbientRunCommand` handles the waited ones (it runs synchronously so the outcome can go in the toast).

Execution is handled by `ScriptRunner`, which launches `pwsh.exe` (or `powershell.exe`) with `-STA -NoProfile -ExecutionPolicy Bypass`. When the `PaletteScriptAttributes.psm1` module is present alongside the script, the runner imports it and dot-sources the script so the custom attributes resolve and the helper functions (clipboard, logging) are available; the information stream is redirected to stdout so `Write-Host` output is captured.

While the runner waits on a script, it shows a **"Running &lt;script&gt;…"** spinner in the palette's status bar and clears it when the script finishes. This is built into `ScriptRunner` rather than per-script, so every waited output mode gets the feedback for free — a slow script no longer looks frozen until its toast or page appears.

If a script declares `[ConfirmBeforeRun('message')]`, selecting it first shows a confirmation dialog carrying that message; the script only runs if you accept. For a parameterized script the form is collected first, then the confirmation appears on submit. This pairs naturally with `[RequiresElevation()]` to guard destructive scripts.

Output modes fall into two **dispositions** by where the result lives:

- **Ambient / fire-and-forget** (`None`, `Toast`, `Clipboard`, `Open`, `File`) — the payoff is external or nil (a file/URL opens, the clipboard is set, or nothing). PaletteShell performs the side effect, shows a toast, and **dismisses the palette**. These are for quick scripts.
- **In-palette / display** (`Markdown`, `Result`, `List`) — the payoff is text worth reading, so PaletteShell opens a page that **stays open** and renders it.

PaletteShell waits (up to the declared timeout, or the [default timeout setting](#settings), 30s unless changed) and captures stdout/stderr for every mode except pure `None` with no `[ScriptTimeout]`, which is launched fire-and-forget without waiting.

| Condition | Behavior |
|-----------|----------|
| `[ScriptOutput('None')]` and no `[ScriptTimeout]` | Fire-and-forget — the process is started, a "Script completed" toast is shown, and the palette dismisses. |
| `[ScriptTimeout(ms)]` set | PaletteShell waits up to `ms`, then kills the process tree on timeout. |
| `[ScriptOutput('Clipboard')]` | Captured output is copied to the clipboard; a toast confirms and the palette dismisses. |
| `[ScriptOutput('Toast')]` | Captured output is shown in the toast as the palette dismisses. |
| `[ScriptOutput('Open')]` | The first non-empty output line is opened as a URL, file, or folder path; the palette dismisses. |
| `[ScriptOutput('File')]` | Captured output is written to a temp file and opened in your editor; the palette dismisses. |
| `[ScriptOutput('Markdown')]` | Captured output is rendered as Markdown on its own page, which stays open. |
| `[ScriptOutput('Result')]` | Captured output is shown as a single copyable result on its own page (Enter copies; "Run again" regenerates). |
| `[ScriptOutput('List')]` | Captured output is parsed into a searchable list of selectable items on its own page. |
| `[ConfirmBeforeRun('msg')]` | Selecting the script prompts a yes/no dialog before it runs. |
| `[RequiresElevation()]` / `#Requires -RunAsAdministrator` | The process is launched elevated (`runas`); output capture is unavailable in this mode, so elevation is only compatible with `[ScriptOutput('None')]`. Combined with any other output mode the script is shown as an incompatible row instead of running. |

### Cross-platform clipboard

The bundled `PaletteScriptAttributes.psm1` module exposes `Get-ClipboardText` / `Set-ClipboardText` for script clipboard helpers. The host extension uses native WinRT clipboard APIs when copying captured output to the clipboard.

### Pinning

Use a script item's **Pin to top** context command to keep it above the rest of the list; **Unpin** puts it back. Pinning is a per-user UI preference, so it lives outside the `.ps1` files — toggling a pin never rewrites your script. The pinned set is stored in a plain `pinned.txt` alongside your scripts (one entry per line, keyed by the script's path relative to the scripts folder), so it survives reloads and restarts and travels with the folder if you sync or copy it. Renaming or moving a script simply orphans its old pin, which is harmless. Pointing PaletteShell at a **different** scripts folder (see [Settings](#settings)) works the same way: it's a fresh folder, so it starts with nothing pinned — just re-pin what you need there.

## Settings

PaletteShell's top-level command has a built-in **Settings** page (the gear icon next to it in the Command Palette) with:

| Setting | Purpose |
|---------|---------|
| **Scripts folder** | Where PaletteShell looks for `.ps1` scripts. Changing this doesn't move your existing scripts or `pinned.txt` — run **"Reload scripts"** afterward to point PaletteShell at the new folder. |
| **Default script host** | `Auto` (recommended), `PowerShell 7 (pwsh)`, or `Windows PowerShell 5.1` — used for any script that doesn't declare its own `[ScriptHost(...)]`. |
| **Default timeout** | Milliseconds to wait for a script that doesn't declare its own `[ScriptTimeout(...)]` before treating it as timed out. |
| **Preferred editor** | Command or path used by **"Open in editor"**. Leave blank to fall back to `$VISUAL`, then `$EDITOR`, then Notepad. |

These are extension-wide defaults, not per-script overrides — a script's own `[ScriptHost(...)]`/`[ScriptTimeout(...)]` attributes always win when present. The **"Create new script"** wizard's Host and Timeout fields default to **"Use default (from Settings)"**, so scaffolded scripts pick up whatever you've configured here instead of freezing in a fixed value, unless you explicitly choose otherwise in the wizard.


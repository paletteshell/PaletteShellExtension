# PaletteShellExtension Agent Guide

This repository is the C# Windows Command Palette extension for PaletteShell.
Keep this file short: it is loaded by coding agents for ordinary repo work.

## Route by Task

- Changing the extension itself: read `CONTRIBUTING.md`, then inspect the relevant C# files.
- Authoring or updating PaletteShell `.ps1` scripts: read `docs/PaletteShellScripts.AGENTS.md`.
- Updating script-facing behavior, attributes, output modes, or samples: update `README.md` and `docs/PaletteShellScripts.AGENTS.md` in the same change.
- Parser or attribute work: start with `docs/parser-map.md`.
- Script execution or output behavior: start with `docs/execution-map.md`.
- Command Palette pages, forms, or settings: start with `docs/ui-map.md`.

## Reference Discipline

Do not read split references such as `docs/extension-behavior-reference.md`,
`docs/project-structure-reference.md`, or `docs/PaletteShellScripts.Reference.md` unless the task
specifically needs full reference behavior.
Prefer the short router files and focused maps first, then open the bigger references only for
exact attribute, output-mode, project-structure, or extension behavior details.

Ignore `bin/`, `obj/`, `AppPackages/`, generated assets, and other build output unless the task is
specifically about packaging, generated artifacts, or build-output diagnostics. Do not summarize
generated output when source files or focused docs answer the question.

## Project Map

- `PaletteShellExtension/` - extension source.
- `PaletteShellExtension.Tests/` - xUnit tests.
- `PaletteShellExtension/PowerShellScriptParser.cs` - script metadata and parameter parser.
- `PaletteShellExtension/Classes/ScriptRunner.cs` - PowerShell process execution.
- `PaletteShellExtension/Classes/ScriptOutputHandler.cs` - stdout-to-output-mode handling.
- `PaletteShellExtension/Pages/PaletteShellExtensionPage.cs` - main command list, script discovery, and built-in commands.
- `PaletteShellExtension/Forms/` - generated forms for setup, script parameters, and script creation.
- `PaletteShellExtension/SampleScripts/` - bundled scripts copied to the user's scripts folder.

## Validation

Use x64 unless the task specifically targets ARM64:

```powershell
dotnet build PaletteShellExtension.sln -c Debug -p:Platform=x64
dotnet test PaletteShellExtension.sln -c Debug -p:Platform=x64
```

For parser, manifest, output-mode, or form behavior changes, add or update focused tests in
`PaletteShellExtension.Tests/`.

## Notes

- The extension copies a short script-authoring guide into user script folders as `AGENTS.md`.
  The full reference ships beside it as `PaletteShellScripts.Reference.md`.
- Scripts are top-level `.ps1` files; subfolders are not scanned.
- Script metadata is parsed from text by `PowerShellScriptParser`; do not assume scripts are executed
  just to discover metadata.

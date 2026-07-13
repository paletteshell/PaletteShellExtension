# Parser Map

Use this when changing script metadata, attributes, help parsing, parameter parsing, or compatibility gates.

## Main Files

- `PaletteShellExtension/PowerShellScriptParser.cs` - text parser for help, attributes, and `param(...)`.
- `PaletteShellExtension/Classes/ScriptManifest.cs` - parsed script metadata model.
- `PaletteShellExtension/Classes/ScriptParameter.cs` - parsed parameter metadata.
- `PaletteShellExtension/Classes/ScriptParseResult.cs` - parse success/fail-closed result.
- `PaletteShellExtension/Classes/ScriptCompatibilityValidator.cs` - version, host, pwsh, and elevation compatibility checks.
- `PaletteShellExtension/Classes/AppVersion.cs` - running app version and script version-range checks.
- `PaletteShellExtension/PaletteScriptAttributes.psm1` - runtime definitions for script attributes and helper functions.

## Tests

- `PaletteShellExtension.Tests/PowerShellScriptParserTests.cs`
- `PaletteShellExtension.Tests/ScriptElevationTests.cs`
- `PaletteShellExtension.Tests/AppVersionTests.cs`

## Rules Of Thumb

- The parser reads text; it does not execute scripts to discover metadata.
- Attributes after `param(...)` or inside comments should not affect metadata.
- Malformed metadata should fail closed when running a partially understood script could be risky.
- If script-facing behavior changes, update `docs/PaletteShellScripts.AGENTS.md`, `docs/PaletteShellScripts.Reference.md`, and any relevant sample scripts.

# Contributing to PaletteShell

This covers building, testing, and debugging the **extension itself** (the C# host). If you're
looking to write a PaletteShell *script*, see the [README](README.md#-creating-your-own-scripts)
and [script authoring agent guide](docs/PaletteShellScripts.AGENTS.md) instead — this doc is for people changing the extension's code.

## Prerequisites

- .NET 9 SDK with the Windows 10.0.26100 platform (`dotnet --list-sdks` should show a `9.0.x` entry)
- Windows 10 (10.0.19041) or later
- [Windows Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview)
  (part of PowerToys) installed, for the sideload/debug loop below
- Visual Studio 2022 (17.13+) with the "Windows application development" workload, only needed for
  MSIX packaging and process-attach debugging — everyday build/test/edit works from the CLI alone

## Project layout

Two projects, one solution (`PaletteShellExtension.sln`):

- `PaletteShellExtension/` — the extension itself (see the project structure table in the
  [README](README.md#project-structure) for what each file does)
- `PaletteShellExtension.Tests/` — xUnit tests, currently focused on `PowerShellScriptParser`

Both target `net9.0-windows10.0.26100.0` and only build for `x64`/`ARM64` (no `x86`/`AnyCPU`) —
that's enforced by `Directory.Build.props` at the repo root, so pass `-p:Platform=x64` (or
`ARM64`) to any `dotnet build`/`test` invocation.

## Building and testing

```powershell
dotnet restore PaletteShellExtension.sln
dotnet build PaletteShellExtension.sln -c Debug -p:Platform=x64
dotnet test PaletteShellExtension.sln -c Debug -p:Platform=x64
```

For most changes — anything in `PowerShellScriptParser`, the manifest models, or output
handling — `dotnet test` is the fast loop: it doesn't need Command Palette, sideloading, or a
debugger attached. Add cases to `PaletteShellExtension.Tests/PowerShellScriptParserTests.cs`
alongside behavior changes.

`.github/workflows/ci.yml` runs the same build (both platforms, Release) and test suite on every
PR, so a green `dotnet build`/`dotnet test` locally is a reliable predictor of CI passing.

## Running and debugging the extension locally

Open `PaletteShellExtension.sln` in Visual Studio with `PaletteShellExtension` as the startup
project and press **F5**. VS's single-project MSIX tooling (`EnableMsixTooling`,
`HasPackageAndPublishMenu` in the `.csproj`) handles build, sideload deployment, and registering
the COM server in one step, and arms the debugger for it — when Command Palette activates
PaletteShell, breakpoints just hit. No manual `Attach to Process` needed.

Open Command Palette and search **PaletteShell** to trigger activation. If you're mid-debug
session and want to force a fresh activation after a code change, stop debugging, rebuild, and F5
again — VS redeploys the updated package.

For issues you can't easily catch with a debugger (e.g. something a user reports), check the
extension's log file instead — parse failures, icon-validation warnings, and script execution
errors are written to `%LOCALAPPDATA%\PaletteShell\logs\palette-shell-<yyyyMMdd>.log`
(see `Classes/Log.cs`), which is pruned automatically after 7 days.

### Producing a standalone test package

To hand someone a sideloadable build without Visual Studio attached (or to test the packaged
artifact itself), right-click the `PaletteShellExtension` project → **Publish → Create App
Packages...** → choose **Sideloading** → build for the platform you need (`x64` or `ARM64`). This
produces
`PaletteShellExtension/AppPackages/<platform>/PaletteShellExtension_<version>_<platform>_Test/`,
containing the `.msix` and an `Add-AppDevPackage.ps1` installer script — run that script (as your
normal user, not elevated) to install it. On first use it also installs the developer certificate
needed to trust the unsigned test package.

## Bumping the package version

`AppxPackageVersion` in `PaletteShellExtension.csproj` and `<Identity Version="...">` in
`Package.appxmanifest` must match — bump both together.

## Submitting a change

- Keep `dotnet build` and `dotnet test` (both platforms if the change could plausibly differ
  between them) green before opening a PR; CI re-checks the same thing.
- If you change parsing or manifest behavior, add or update a test in
  `PaletteShellExtension.Tests` rather than relying on manual sideload testing alone.
- If you change end-user-facing behavior (attributes, output modes, sample scripts), update the
  relevant section of [README.md](README.md) and, if it affects script authoring, [docs/PaletteShellScripts.AGENTS.md](docs/PaletteShellScripts.AGENTS.md)
  plus [docs/PaletteShellScripts.Reference.md](docs/PaletteShellScripts.Reference.md)
  in the same PR — they're both meant to stay authoritative.

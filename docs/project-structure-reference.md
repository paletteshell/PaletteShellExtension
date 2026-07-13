# Project Structure Reference

## Building from Source

PaletteShell is a .NET 9 Windows app packaged as an MSIX Command Palette extension. For running
tests, sideloading, and debugging the extension itself, see [CONTRIBUTING.md](../CONTRIBUTING.md).

**Requirements**

- .NET 9 SDK with the Windows 10.0.26100 platform
- Windows 10 (10.0.19041) or later
- [Windows Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) (PowerToys) installed

**Build**

```powershell
dotnet build PaletteShellExtension/PaletteShellExtension.csproj
```

### Project Structure

| Path | Responsibility |
|------|----------------|
| `PaletteShellExtension.cs` | Extension entry point; provides the commands provider to Command Palette |
| `PaletteShellExtensionCommandsProvider.cs` | Registers the top-level PaletteShell command |
| `Pages/PaletteShellExtensionPage.cs` | Main list page — discovery, sample/module/`AGENTS.md` copying, item building |
| `Classes/SampleScriptInstaller.cs` | Tracks the hash of each written sample plus the last synced app version (in `sample-scripts.json`) so an unchanged install can skip the sample sync |
| `PowerShellScriptParser.cs` | Parses script metadata and parameters with a lightweight text parser |
| `Classes/ScriptManifest.cs`, `ScriptParameter.cs` | Parsed metadata models |
| `Classes/ScriptRunner.cs` | Builds the process and runs scripts (fire-and-forget or wait-and-capture); preflights `[RequiresModule]` dependencies |
| `Classes/ScriptRunDispatcher.cs` | Decides the side effect for a completed run per its output mode (set clipboard, open target, open output) and returns a surface-agnostic outcome |
| `Classes/ScriptOutputHandler.cs` | Low-level output helpers — set the clipboard, open a target, decide whether a target is safe to open unprompted |
| `Classes/AmbientRunner.cs` | Runs an ambient (fire-and-forget) script synchronously, performs its side effect, and returns a toast that dismisses the palette |
| `Classes/ScriptFailureReport.cs` | Models a failed run for `ScriptFailurePresenter` to surface |
| `Classes/ScriptElevation.cs` | Detects elevation requirement and gates it against incompatible output modes |
| `Classes/ScriptStatus.cs` | Shows the "Running…" spinner in the status bar while a script runs |
| `Classes/PinnedScripts.cs` | Tracks pinned scripts (persisted to `pinned.txt`) so they sort to the top |
| `Classes/InstalledCommunityScripts.cs` | Records which local scripts came from the community catalog so update checks can compare shas |
| `Classes/AppVersion.cs` | Resolves the running PaletteShell version and checks a script's version range |
| `Classes/RecycleBin.cs` | Sends a deleted script to the Windows Recycle Bin via `SHFileOperation` |
| `Classes/EditorLauncher.cs` | Opens a script in the preferred editor setting, `$VISUAL`/`$EDITOR`, or Notepad |
| `Classes/PowerShellQuoting.cs` | Quotes/escapes form values passed to the script (unless `[AllowExpression()]`) |
| `Classes/Log.cs` | Lightweight diagnostic logging |
| `Classes/PaletteShellSettingsManager.cs` | Backs the Settings page (scripts folder, default host, default timeout, preferred editor) and persists it to `settings.json` |
| `Pages/ScriptsFolderSetupPage.cs`, `Forms/ScriptsFolderSetupForm.cs` | First-run (and re-run) prompt that collects the scripts folder |
| `Commands/RunScriptCommand.cs` | Runs a pure `None` + no-timeout script fire-and-forget (nothing to wait for) with a "Script completed" toast |
| `Commands/AmbientRunCommand.cs` | Runs a waited ambient script (Toast/Clipboard/Open/File, or None with a timeout), performs the side effect, and toasts the outcome as the palette dismisses |
| `Commands/CallbackCommand.cs` | Wraps a callback as a command — the confirmed action behind a confirmation dialog |
| `Commands/TogglePinCommand.cs` | Pins/unpins a script and refreshes the list |
| `Commands/DeleteScriptCommand.cs` | Deletes a script to the Recycle Bin after a confirmation dialog |
| `Commands/RevealInExplorerCommand.cs` | Opens File Explorer with the script file selected |
| `Commands/OpenInEditorCommand.cs`, `OpenFolderCommand.cs`, `OpenLinkCommand.cs`, `ReloadPageCommand.cs` | Built-in and per-item commands |
| `Commands/LaunchCommunityStoreCommand.cs` | "Browse community scripts" — opens the Script Manager, falling back to the GitHub repo |
| `Commands/IncompatibleScriptCommand.cs`, `ElevationIncompatibleCommand.cs` | Shown in place of running when a script's version range or elevation/output-mode combo can't run |
| `Commands/ScriptFailurePresenter.cs` | Surfaces a failed run (`ScriptFailureReport`) to the user |
| `Pages/ScriptParameterFormPage.cs`, `Forms/ScriptParameterForm.cs` | Auto-generated input form for parameterized scripts |
| `Pages/ScriptMarkdownPage.cs` | Runs a script and renders its output as Markdown |
| `Pages/ScriptListPage.cs` | Runs a script and turns its stdout into a searchable, pickable list |
| `Pages/ScriptResultPage.cs` | Runs a script and shows its output as a single copyable result (Enter copies; Run again regenerates) |
| `Commands/CopyValueCommand.cs` | Copies a List item's value to the clipboard when picked |
| `Pages/NewScriptWizardPage.cs`, `Forms/NewScriptWizardForm.cs` | "Create new script" scaffolding wizard |
| `PaletteScriptAttributes.psm1` | PowerShell module defining the metadata attributes and clipboard/logging helpers |
| `SampleScripts/` | Embedded sample scripts copied to the user's scripts folder |


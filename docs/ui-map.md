# UI Map

Use this when changing Command Palette pages, forms, list items, settings, pinning, script creation, or context commands.

## Main Files

- `PaletteShellExtension/Pages/PaletteShellExtensionPage.cs` - main list, script discovery, built-in actions, item routing.
- `PaletteShellExtension/Pages/ScriptsFolderSetupPage.cs` - first-run and folder setup page.
- `PaletteShellExtension/Pages/ScriptParameterFormPage.cs` - parameter form page wrapper.
- `PaletteShellExtension/Pages/ScriptMarkdownPage.cs` - Markdown output page.
- `PaletteShellExtension/Pages/ScriptResultPage.cs` - single result page.
- `PaletteShellExtension/Pages/ScriptListPage.cs` - searchable list and live provider output.
- `PaletteShellExtension/Pages/NewScriptWizardPage.cs` - new-script wizard page.
- `PaletteShellExtension/Forms/` - form definitions and field mapping.
- `PaletteShellExtension/Commands/` - built-in and per-script commands.
- `PaletteShellExtension/Classes/PinnedScripts.cs` - pin storage and ordering.
- `PaletteShellExtension/Classes/PaletteShellSettingsManager.cs` - settings persistence.

## Tests

- `PaletteShellExtension.Tests/ScriptParameterFormTests.cs`
- `PaletteShellExtension.Tests/NewScriptWizardFormTests.cs`
- `PaletteShellExtension.Tests/InstalledCommunityScriptsTests.cs`
- Parser/execution tests may also be needed when UI routes depend on manifest behavior.

## Rules Of Thumb

- Keep startup resilient: folder errors should show recoverable setup/retry UI instead of crashing COM activation.
- Keep script rows visible when possible, even for parse or compatibility failures, so users can edit/delete them.
- Display modes stay in the palette; ambient modes perform the side effect and dismiss.
- Do not duplicate the same status or identity in multiple visible places.

# Execution Map

Use this when changing how scripts run, time out, capture output, open files/URLs, copy results, or surface failures.

## Main Files

- `PaletteShellExtension/Classes/ScriptExecutionService.cs` - creates execution plans from manifests.
- `PaletteShellExtension/Classes/ScriptExecutionPlan.cs` - resolved host, cwd, env, timeout, and output decisions.
- `PaletteShellExtension/Classes/ScriptRunner.cs` - process launch, wait/capture, module import, dependency preflight.
- `PaletteShellExtension/Classes/ScriptRunDispatcher.cs` - maps completed runs to side effects and copy/open outcomes.
- `PaletteShellExtension/Classes/ScriptOutputHandler.cs` - clipboard, file, and open helpers.
- `PaletteShellExtension/Classes/AmbientRunner.cs` - waited ambient output modes that dismiss with a toast.
- `PaletteShellExtension/Classes/ScriptFailureReport.cs` - failure details shown to users.
- `PaletteShellExtension/Commands/RunScriptCommand.cs` - pure `None` with no timeout.
- `PaletteShellExtension/Commands/AmbientRunCommand.cs` - waited ambient modes.
- `PaletteShellExtension/Commands/ScriptFailurePresenter.cs` - user-facing failure display.

## Tests

- `PaletteShellExtension.Tests/ScriptRunnerTests.cs`
- `PaletteShellExtension.Tests/ScriptOutputHandlerTests.cs`
- `PaletteShellExtension.Tests/ScriptFailureReportTests.cs`
- `PaletteShellExtension.Tests/ResilienceTests.cs`

## Rules Of Thumb

- `None` with no timeout is fire-and-forget; other output modes wait and capture.
- Elevated scripts cannot capture output, so elevation only works with `None`.
- `Open` should treat unsafe targets carefully and preserve confirmation actions.
- Failure paths should report the real launch, timeout, exit, or dependency problem.

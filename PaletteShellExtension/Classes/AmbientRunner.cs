using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Commands;
using System;
using System.IO;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Runs an ambient (fire-and-forget) script and returns a <see cref="CommandResult"/> reporting the
/// outcome. Ambient modes' payoff is external or nil (set the clipboard, open a target, open output
/// in an editor, or nothing), so successful runs are surfaced as toasts while terminal failures use
/// the common failure dialog.
///
/// The toast dismisses the palette (the toolkit default for <see cref="CommandResult.ShowToast(string)"/>),
/// so an ambient run ends the way it did before the async refactor — run, toast, done — rather than
/// parking on a status page the user has to dismiss.
///
/// The run is synchronous and bounded by the plan's timeout so the returned result can carry the
/// real outcome. Ambient modes are for quick scripts; anything that produces slow or rich output
/// uses a display mode (Result/Markdown/List), which stays on its own async page instead.
/// </summary>
internal static class AmbientRunner
{
    public static CommandResult RunAndToast(
        ScriptExecutionPlan plan,
        ScriptManifest manifest,
        string args = "")
    {
        ScriptRunner.ScriptResult? result;
        try
        {
            result = ScriptExecutionService.RunAndWait(plan, args);
        }
        catch (Exception ex)
        {
            return ScriptFailurePresenter.ToCommandResult(plan.ScriptPath, plan.Host, args, ex);
        }

        if (result is null || result.TimedOut || result.ExitCode != 0)
        {
            return ScriptFailurePresenter.ToCommandResult(plan.ScriptPath, plan.Host, args, result);
        }

        return ResultForCompletedRun(plan, manifest, result, args);
    }

    internal static CommandResult ResultForCompletedRun(
        ScriptExecutionPlan plan,
        ScriptManifest manifest,
        ScriptRunner.ScriptResult? result,
        string args = "")
    {
        if (result is null || result.TimedOut || result.ExitCode != 0)
        {
            return ScriptFailurePresenter.ToCommandResult(plan.ScriptPath, plan.Host, args, result);
        }

        ScriptRunDispatcher.RunOutcome outcome;
        try
        {
            // Elevated runs can't capture output; the compatibility gate keeps capturing modes off
            // elevation, so this is only null for None-mode elevated runs.
            var scriptName = Path.GetFileNameWithoutExtension(plan.ScriptPath);
            var output = plan.CaptureOutput ? result.StandardOutput : null;
            outcome = ScriptRunDispatcher.Apply(manifest, output, scriptName);
        }
        catch (Exception ex)
        {
            return ScriptFailurePresenter.ToCommandResult(plan.ScriptPath, plan.Host, args, ex);
        }

        // A non-web, non-file open target: name it in the toast rather than launching it silently.
        if (outcome.UnsafeOpenTarget is { } target)
        {
            return Toast($"Script output isn't a safe target to open: {target}");
        }

        return Toast(outcome.Status);
    }

    /// <summary>Fire-and-forget toast: shows the outcome and dismisses the palette (the toolkit
    /// default), so an ambient run ends the way it did before the async refactor — run, toast, done.</summary>
    public static CommandResult Toast(string message)
        => CommandResult.ShowToast(message);
}

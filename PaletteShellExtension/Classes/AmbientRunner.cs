using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Runs an ambient (fire-and-forget) script and returns a toast <see cref="CommandResult"/>
/// reporting the outcome. Ambient modes' payoff is external or nil (set the clipboard, open a
/// target, open output in an editor, or nothing) — there's no page to render into, so the result is
/// surfaced as a toast.
///
/// The toast dismisses the palette (the toolkit default for <see cref="CommandResult.ShowToast(string)"/>),
/// so an ambient run ends the way it did before the async refactor — run, toast, done — rather than
/// parking on a status page the user has to dismiss.
///
/// The run is synchronous and bounded by the plan's timeout so the toast can carry the real outcome.
/// Ambient modes are for quick scripts; anything that produces slow or rich output uses a display
/// mode (Result/Markdown/List), which stays on its own async page instead.
/// </summary>
internal static class AmbientRunner
{
    public static CommandResult RunAndToast(
        ScriptExecutionPlan plan,
        ScriptManifest manifest,
        string scriptName,
        string args = "")
    {
        ScriptRunner.ScriptResult? result;
        try
        {
            result = ScriptExecutionService.RunAndWait(plan, args);
        }
        catch (Exception ex)
        {
            return Toast($"Error running {scriptName}: {ex.Message}");
        }

        if (result is null || result.TimedOut || result.ExitCode != 0)
        {
            return Toast(FailureMessage(result, scriptName));
        }

        // Elevated runs can't capture output; the compatibility gate keeps capturing modes off
        // elevation, so this is only null for None-mode elevated runs.
        var output = plan.CaptureOutput ? result.StandardOutput : null;
        var outcome = ScriptRunDispatcher.Apply(manifest, output, scriptName);

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

    private static string FailureMessage(ScriptRunner.ScriptResult? result, string scriptName)
    {
        if (result is null)
        {
            return $"Failed to start {scriptName}.";
        }

        if (result.TimedOut)
        {
            return $"{scriptName} timed out.";
        }

        var error = result.StandardError?.Trim();
        return string.IsNullOrEmpty(error)
            ? $"{scriptName} failed with exit code {result.ExitCode}."
            : $"{scriptName} failed: {error}";
    }
}

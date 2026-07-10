namespace PaletteShellExtension.Classes;

/// <summary>
/// Decides what to do with a completed script run according to its declared
/// <c>[ScriptOutput(...)]</c> mode, and performs the terminal side effect (set the clipboard,
/// open the output in an editor, open a safe target). It returns a surface-agnostic
/// <see cref="RunOutcome"/> so both the async <see cref="Pages.ScriptRunPage"/> (a list surface)
/// and the parameter form's in-place render (a Markdown surface) share one set of decisions.
///
/// This is the async counterpart of the old synchronous output handling: it only ever runs after
/// the script has finished on a background thread, never on the thread the host is blocked on for
/// its COM call.
/// </summary>
internal static class ScriptRunDispatcher
{
    /// <summary>
    /// The result of dispatching a successful run. The non-interactive side effect (clipboard,
    /// editor, safe open) has already happened by the time this is returned.
    /// <see cref="Status"/> is a short human message (for Markdown modes it is the rendered body).
    /// <see cref="CopyValue"/>, when set, is text an interactive surface can offer to (re)copy.
    /// <see cref="UnsafeOpenTarget"/>, when set, is an open target that must be confirmed before
    /// launching (a non-web, non-file path) — nothing has been opened yet.
    /// </summary>
    internal readonly record struct RunOutcome(string Status, string? CopyValue, string? UnsafeOpenTarget);

    /// <summary>Performs the declared output effect for a completed, successful run and returns
    /// what to show. <paramref name="output"/> is the captured stdout (null for elevated runs,
    /// which can't be captured).</summary>
    public static RunOutcome Apply(ScriptManifest manifest, string? output, string scriptName)
    {
        switch (manifest.Output?.Trim().ToLowerInvariant())
        {
            case "clipboard":
                if (!string.IsNullOrEmpty(output))
                {
                    ScriptOutputHandler.TrySetClipboard(output);
                    return new RunOutcome("Copied to clipboard", output, null);
                }
                return new RunOutcome("Script completed", null, null);

            // Write stdout to a temp file and open it in the user's editor.
            case "file":
                if (!string.IsNullOrEmpty(output))
                {
                    EditorLauncher.OpenContent(output, manifest.FileExtension, scriptName);
                    return new RunOutcome("Opened output in editor", null, null);
                }
                return new RunOutcome("Script completed", null, null);

            // Treat the first non-empty line of stdout as a URL/file/folder and let Windows open
            // it. Web links and real files/folders open unprompted; anything else is returned as
            // an UnsafeOpenTarget so the caller can confirm the exact target first.
            case "open":
                var target = ScriptOutputHandler.GetOpenTarget(output);
                if (target is null)
                {
                    return new RunOutcome("Script completed without an open target", null, null);
                }
                if (ScriptOutputHandler.IsSafeOpenTarget(target))
                {
                    ScriptOutputHandler.OpenTarget(target);
                    return new RunOutcome($"Opened {target}", null, null);
                }
                return new RunOutcome($"Open {target}?", null, target);

            // Run silently: report completion without surfacing output.
            case "none":
                return new RunOutcome("Script completed", null, null);

            // Only reached on the parameter form's in-place path (no-parameter Markdown routes to
            // ScriptMarkdownPage). The raw stdout is the Markdown body to render.
            case "markdown":
                return new RunOutcome(
                    string.IsNullOrWhiteSpace(output) ? "_Script completed with no output._" : output!,
                    null,
                    null);

            // "toast" (and any unrecognized value) surfaces the captured output.
            default:
                var value = output?.Trim();
                return string.IsNullOrEmpty(value)
                    ? new RunOutcome("Script completed", null, null)
                    : new RunOutcome(value!, value, null);
        }
    }
}

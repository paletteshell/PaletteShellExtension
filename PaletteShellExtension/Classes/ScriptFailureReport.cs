using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Builds the full plain-text failure report a user can open in their editor when a script
/// fails — the untruncated counterpart to the 300-char summary in
/// <see cref="ScriptRunner.DescribeFailure"/>. Pure string formatting so it stays unit-testable
/// without spawning a process.
/// </summary>
internal static class ScriptFailureReport
{
    // Matches a PowerShell switch whose name contains a secret-ish keyword, plus the token
    // that follows it — a single-quoted value ('' = escaped quote) or a bare token (numbers,
    // bools, expressions). The value is masked so tokens/passwords/keys don't land in the
    // plaintext report. `key` is deliberately broad (also matches -Keyword etc.): over-
    // redaction is harmless here, under-redaction leaks secrets.
    private static readonly Regex SensitiveArg = new(
        @"-(\w*(?:password|token|secret|credential|apikey|pwd|key)\w*)\s+('(?:[^']|'')*'|\S+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Masks values of arguments whose switch name looks like a secret
    /// (password/token/secret/credential/apikey/pwd/key), leaving the switch name intact:
    /// <c>-Token 'abc'</c> becomes <c>-Token '***'</c>.</summary>
    internal static string RedactArgs(string args) =>
        SensitiveArg.Replace(args, "-$1 '***'");

    /// <summary>One-line outcome, e.g. "exited with code 1", "timed out and was killed",
    /// "failed to start". Shared by the report header and the failure dialog title.</summary>
    public static string DescribeOutcome(ScriptRunner.ScriptResult? result)
    {
        if (result is null)
            return "failed to start";

        if (result.TimedOut)
            return "timed out and was killed";

        return $"exited with code {result.ExitCode}";
    }

    /// <summary>
    /// Full failure report: script path, resolved shell, args, outcome, duration (when
    /// known), and the complete stderr and stdout streams.
    /// </summary>
    public static string Build(string scriptPath, string host, string args, ScriptRunner.ScriptResult? result)
    {
        var name = Path.GetFileNameWithoutExtension(scriptPath);
        var sb = new StringBuilder();

        var culture = CultureInfo.InvariantCulture;
        sb.AppendLine(culture, $"Script failure report — {name}");
        sb.AppendLine(culture, $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine(culture, $"Script:   {scriptPath}");
        sb.AppendLine(culture, $"Shell:    {ScriptRunner.ResolveShell(host)}");
        sb.AppendLine(culture, $"Args:     {(string.IsNullOrWhiteSpace(args) ? "(none)" : RedactArgs(args))}");
        sb.AppendLine(culture, $"Outcome:  {DescribeOutcome(result)}");
        if (result?.DurationMs is { } duration)
        {
            sb.AppendLine(culture, $"Duration: {duration} ms");
        }
        sb.AppendLine();
        sb.AppendLine("----- stderr -----");
        sb.AppendLine(ContentOrPlaceholder(result?.StandardError));
        sb.AppendLine();
        sb.AppendLine("----- stdout -----");
        sb.AppendLine(ContentOrPlaceholder(result?.StandardOutput));
        sb.AppendLine();
        sb.AppendLine(culture, $"Logs: {Log.LogDirectory}");
        sb.AppendLine(culture, $"This report: {EditorLauncher.OutputDirectory} (auto-cleaned when enabled in settings)");

        return sb.ToString();
    }

    /// <summary>
    /// Trims stderr for inclusion in a single log line: newlines collapsed, truncated to
    /// <paramref name="maxChars"/>. Non-empty results come back prefixed with "; stderr: "
    /// so callers can append directly to an existing message; empty input yields "".
    /// </summary>
    public static string StderrForLog(string? stderr, int maxChars = 2000)
    {
        var text = stderr?.Trim();
        if (string.IsNullOrEmpty(text))
            return "";

        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", " | ");
        if (text.Length > maxChars)
        {
            text = text[..maxChars] + "…";
        }
        return $"; stderr: {text}";
    }

    private static string ContentOrPlaceholder(string? stream)
    {
        var text = stream?.Trim();
        return string.IsNullOrEmpty(text) ? "(empty)" : text;
    }
}

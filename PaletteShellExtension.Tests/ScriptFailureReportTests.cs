using System;
using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class ScriptFailureReportTests
{
    [Fact]
    public void DescribeOutcome_NullResult_SaysFailedToStart()
    {
        Assert.Equal("failed to start", ScriptFailureReport.DescribeOutcome((ScriptRunner.ScriptResult?)null));
    }

    [Fact]
    public void DescribeOutcome_TimedOut_SaysTimedOut()
    {
        var result = new ScriptRunner.ScriptResult { TimedOut = true };
        Assert.Equal("timed out and was killed", ScriptFailureReport.DescribeOutcome(result));
    }

    [Fact]
    public void DescribeOutcome_NonZeroExit_IncludesExitCode()
    {
        var result = new ScriptRunner.ScriptResult { ExitCode = 42 };
        Assert.Equal("exited with code 42", ScriptFailureReport.DescribeOutcome(result));
    }

    [Fact]
    public void Build_ExitCodeFailure_IncludesPathArgsExitCodeAndFullStreams()
    {
        var longError = new string('e', 5000);
        var result = new ScriptRunner.ScriptResult
        {
            ExitCode = 1,
            StandardError = longError,
            StandardOutput = "partial output",
            DurationMs = 42,
        };

        var report = ScriptFailureReport.Build(@"C:\Scripts\foo.ps1", "pwsh", "-Name 'x'", result);

        Assert.Contains(@"C:\Scripts\foo.ps1", report);
        Assert.Contains("-Name 'x'", report);
        Assert.Contains("exited with code 1", report);
        Assert.Contains("Duration: 42 ms", report);
        Assert.Contains(longError, report); // full stderr, no truncation
        Assert.Contains("partial output", report);
    }

    [Fact]
    public void Build_NullResult_SaysFailedToStartWithEmptyStreams()
    {
        var report = ScriptFailureReport.Build(@"C:\Scripts\foo.ps1", "pwsh", "", (ScriptRunner.ScriptResult?)null);

        Assert.Contains("failed to start", report);
        Assert.Contains("Args:     (none)", report);
        Assert.Contains("(empty)", report);
        Assert.DoesNotContain("Duration:", report);
    }

    [Fact]
    public void Build_TimedOut_SaysTimedOut()
    {
        var result = new ScriptRunner.ScriptResult { TimedOut = true, DurationMs = 30000 };
        var report = ScriptFailureReport.Build(@"C:\Scripts\slow.ps1", "pwsh", "", result);

        Assert.Contains("timed out and was killed", report);
        Assert.Contains("Duration: 30000 ms", report);
    }

    [Fact]
    public void DescribeOutcome_Exception_IncludesMessage()
    {
        var exception = new InvalidOperationException("clipboard unavailable");

        Assert.Equal("failed: clipboard unavailable", ScriptFailureReport.DescribeOutcome(exception));
    }

    [Fact]
    public void Build_ExceptionFailure_IncludesPathArgsAndException()
    {
        var exception = new InvalidOperationException("clipboard unavailable");

        var report = ScriptFailureReport.Build(@"C:\Scripts\foo.ps1", "pwsh", "-Token 'secret'", exception);

        Assert.Contains(@"C:\Scripts\foo.ps1", report);
        Assert.Contains("-Token '***'", report);
        Assert.Contains("failed: clipboard unavailable", report);
        Assert.Contains("InvalidOperationException", report);
        Assert.DoesNotContain("secret", report);
    }

    [Fact]
    public void StderrForLog_Empty_ReturnsEmpty()
    {
        Assert.Equal("", ScriptFailureReport.StderrForLog(null));
        Assert.Equal("", ScriptFailureReport.StderrForLog(""));
        Assert.Equal("", ScriptFailureReport.StderrForLog("   "));
    }

    [Fact]
    public void StderrForLog_MultiLine_CollapsesNewlinesIntoOneLine()
    {
        var log = ScriptFailureReport.StderrForLog("line one\r\nline two\nline three");

        Assert.Equal("; stderr: line one | line two | line three", log);
        Assert.DoesNotContain("\n", log);
    }

    [Fact]
    public void StderrForLog_LongInput_TruncatesToMax()
    {
        var log = ScriptFailureReport.StderrForLog(new string('x', 5000), maxChars: 100);

        Assert.Contains(new string('x', 100) + "…", log);
        Assert.DoesNotContain(new string('x', 101), log);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("plain text", "plain text")]
    // pwsh 7's colored Write-Error output: CSI color sequences.
    [InlineData("\x1B[31;1mWrite-Error: \x1B[31;1mboom\x1B[0m", "Write-Error: boom")]
    // Cursor movement and erase sequences.
    [InlineData("progress\x1B[2K\x1B[1Gdone", "progressdone")]
    // OSC hyperlink (terminated by ESC \ string terminator).
    [InlineData("\x1B]8;;https://example.com\x1B\\link\x1B]8;;\x1B\\", "link")]
    public void StripAnsi_RemovesEscapeSequences(string? input, string? expected)
    {
        Assert.Equal(expected, ScriptRunner.StripAnsi(input));
    }
}

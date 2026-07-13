using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class AmbientRunnerTests
{
    private const string ScriptPath = @"C:\scripts\toast.ps1";

    private static ScriptExecutionPlan ToastPlan() => new()
    {
        ScriptPath = ScriptPath,
        Host = "pwsh",
        Env = new(),
        OutputMode = "Toast",
    };

    private static ScriptManifest ToastManifest() => new()
    {
        Output = "Toast",
        Title = "Toast",
    };

    [Fact]
    public void ResultForCompletedRun_NullStartResult_ShowsFailureDialog()
    {
        var result = AmbientRunner.ResultForCompletedRun(
            ToastPlan(),
            ToastManifest(),
            null);

        var args = AssertFailureConfirm(result);
        Assert.Contains("failed to start", args.Title);
        Assert.Equal("The script process could not be started.", args.Description);
    }

    [Fact]
    public void ResultForCompletedRun_Timeout_ShowsFailureDialog()
    {
        var result = AmbientRunner.ResultForCompletedRun(
            ToastPlan(),
            ToastManifest(),
            new ScriptRunner.ScriptResult { TimedOut = true });

        var args = AssertFailureConfirm(result);
        Assert.Contains("timed out and was killed", args.Title);
    }

    [Fact]
    public void ResultForCompletedRun_NonZeroExit_ShowsFailureDialog()
    {
        var result = AmbientRunner.ResultForCompletedRun(
            ToastPlan(),
            ToastManifest(),
            new ScriptRunner.ScriptResult { ExitCode = 7, StandardError = "boom" });

        var args = AssertFailureConfirm(result);
        Assert.Contains("exited with code 7", args.Title);
        Assert.Contains("boom", args.Description);
    }

    [Fact]
    public void ResultForCompletedRun_SuccessfulToast_StillShowsToast()
    {
        var result = AmbientRunner.ResultForCompletedRun(
            ToastPlan(),
            ToastManifest(),
            new ScriptRunner.ScriptResult { ExitCode = 0, StandardOutput = "hello" });

        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        var args = Assert.IsType<ToastArgs>(result.Args);
        Assert.Equal("hello", args.Message);
    }

    // The Command Palette host's command-result switch (ShellViewModel) has no GoToPage case, so
    // a failure returned from Invoke() must be a Confirm dialog — the kind the host actually
    // renders — not a page navigation.
    private static ConfirmationArgs AssertFailureConfirm(CommandResult result)
    {
        Assert.Equal(CommandResultKind.Confirm, result.Kind);
        var args = Assert.IsType<ConfirmationArgs>(result.Args);
        Assert.NotNull(args.PrimaryCommand);
        return args;
    }
}

using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class ScriptElevationTests
{
    private static ScriptManifest Manifest(bool? admin, string? output) =>
        new() { RequiresAdmin = admin, Output = output! };

    [Theory]
    // Elevated + capturing output modes are impossible → incompatible.
    [InlineData(true, "Toast", true)]
    [InlineData(true, "Clipboard", true)]
    [InlineData(true, "Markdown", true)]
    [InlineData(true, "Result", true)]
    [InlineData(true, "List", true)]
    [InlineData(true, "Open", true)]
    [InlineData(true, "File", true)]
    [InlineData(true, "File:csv", true)]
    // Elevated + None is the one allowed combination.
    [InlineData(true, "None", false)]
    [InlineData(true, "none", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "", false)]
    // Non-elevated is never blocked, regardless of output mode.
    [InlineData(false, "Toast", false)]
    [InlineData(false, "Markdown", false)]
    [InlineData(false, "None", false)]
    [InlineData(null, "List", false)]
    public void IsElevatedOutputIncompatible_MatchesCapabilityMatrix(bool? admin, string? output, bool expected)
    {
        Assert.Equal(expected, ScriptElevation.IsElevatedOutputIncompatible(Manifest(admin, output)));
    }

    [Theory]
    [InlineData("None", false)]
    [InlineData("none", false)]
    [InlineData("NONE", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Toast", true)]
    [InlineData("Markdown", true)]
    [InlineData("File:json", true)]
    public void CapturesOutput_TrueForEveryModeExceptNone(string? output, bool expected)
    {
        Assert.Equal(expected, ScriptElevation.CapturesOutput(Manifest(false, output)));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void RequiresElevation_ReflectsRequiresAdmin(bool? admin, bool expected)
    {
        Assert.Equal(expected, ScriptElevation.RequiresElevation(Manifest(admin, "None")));
    }
}

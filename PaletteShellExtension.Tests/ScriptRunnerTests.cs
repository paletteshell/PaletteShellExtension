using System.IO;
using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class ScriptRunnerTests
{
    private static string CommandString(System.Diagnostics.ProcessStartInfo psi) =>
        psi.ArgumentList[psi.ArgumentList.Count - 1];

    [Theory]
    [InlineData("a'b", "'a''b'")]
    [InlineData("plain", "'plain'")]
    [InlineData("", "''")]
    [InlineData(null, "''")]
    [InlineData("O'Brien's", "'O''Brien''s'")]
    public void SingleQuote_DoublesEmbeddedQuotes(string? input, string expected)
    {
        Assert.Equal(expected, PowerShellQuoting.SingleQuote(input));
    }

    [Fact]
    public void BuildProcessStartInfo_EscapesApostropheInScriptPath()
    {
        var psi = ScriptRunner.BuildProcessStartInfo(
            scriptPath: @"C:\Users\Sean's PC\s.ps1",
            args: "",
            host: "powershell",
            cwd: null);

        var cmd = CommandString(psi);
        Assert.Contains(@". 'C:\Users\Sean''s PC\s.ps1'", cmd);
    }

    [Fact]
    public void BuildProcessStartInfo_NormalPath_SingleQuotedOnce()
    {
        var psi = ScriptRunner.BuildProcessStartInfo(
            scriptPath: @"C:\scripts\test.ps1",
            args: "",
            host: "powershell",
            cwd: null);

        var cmd = CommandString(psi);
        Assert.Contains(@". 'C:\scripts\test.ps1'", cmd);
        Assert.DoesNotContain("''", cmd);
    }

    [Fact]
    public void BuildProcessStartInfo_EscapesApostropheInModulePath()
    {
        // Module segment only emitted when the .psm1 exists next to the script, so use a temp
        // dir with an apostrophe in the path and drop the module file there.
        var dir = Path.Combine(Path.GetTempPath(), "PSE'Test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "PaletteScriptAttributes.psm1"), "");
            var scriptPath = Path.Combine(dir, "s.ps1");

            var psi = ScriptRunner.BuildProcessStartInfo(
                scriptPath: scriptPath,
                args: "",
                host: "powershell",
                cwd: null);

            var cmd = CommandString(psi);
            var expectedModule = Path.Combine(dir, "PaletteScriptAttributes.psm1").Replace("'", "''");
            Assert.Contains($"using module '{expectedModule}';", cmd);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

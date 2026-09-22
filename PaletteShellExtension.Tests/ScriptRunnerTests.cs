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

    [Fact]
    public void ParseHost_RecognizesStandardAndCustomPaths()
    {
        Assert.Equal(ScriptRunner.ShellHost.Auto, ScriptRunner.ParseHost("auto"));
        Assert.Equal(ScriptRunner.ShellHost.Auto, ScriptRunner.ParseHost(null));
        Assert.Equal(ScriptRunner.ShellHost.Pwsh, ScriptRunner.ParseHost("pwsh"));
        Assert.Equal(ScriptRunner.ShellHost.WindowsPowerShell, ScriptRunner.ParseHost("powershell"));
        Assert.Equal(ScriptRunner.ShellHost.CustomPath, ScriptRunner.ParseHost(@"C:\Users\User\AppData\Local\Microsoft\WindowsApps\Microsoft.PowerShell_8wekyb3d8bbwe\pwsh.exe"));
        Assert.Equal(ScriptRunner.ShellHost.CustomPath, ScriptRunner.ParseHost("pwsh.exe"));
        Assert.Equal(ScriptRunner.ShellHost.Unknown, ScriptRunner.ParseHost("bash"));
    }

    [Fact]
    public void ResolveShell_CustomPath_ResolvesExistingOrThrows()
    {
        var tempExe = Path.GetTempFileName();
        try
        {
            var resolved = ScriptRunner.ResolveShell(tempExe);
            Assert.Equal(tempExe, resolved);

            var missingExe = Path.Combine(Path.GetTempPath(), "nonexistent_pwsh_" + System.Guid.NewGuid().ToString("N") + ".exe");
            Assert.Throws<ScriptRunner.ShellResolutionException>(() => ScriptRunner.ResolveShell(missingExe));
        }
        finally
        {
            if (File.Exists(tempExe))
            {
                File.Delete(tempExe);
            }
        }
    }

    [Fact]
    public void PwshInstallDirs_IncludesWindowsAppsLocation()
    {
        var dirs = System.Linq.Enumerable.ToList(ScriptRunner.PwshInstallDirs());
        Assert.Contains(dirs, d => d.Contains("WindowsApps", System.StringComparison.OrdinalIgnoreCase));
    }
}

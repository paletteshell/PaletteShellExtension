using System.IO;
using PaletteShellExtension;
using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class ScriptVersionStamperTests
{
    [Fact]
    public void AddsVersion_ImmediatelyBeforeParam_WhenMissing()
    {
        using var file = new TestScriptFile("[ScriptGroup('Tools')]\n[CmdletBinding()]\nparam()\n\nWrite-Host 'hi'\n");

        var stamped = ScriptVersionStamper.TryStamp(file.Path);

        Assert.True(stamped);
        var text = File.ReadAllText(file.Path);
        Assert.Contains("[ScriptVersion('1.0.0')]", text);
        // Inserted at the param block, after the pre-existing attributes.
        Assert.Matches(@"\[CmdletBinding\(\)\]\s*\[ScriptVersion\('1\.0\.0'\)\]\s*param\(", text);
        // The body is untouched.
        Assert.Contains("Write-Host 'hi'", text);
    }

    [Fact]
    public void StampedScript_ParsesToVersion1_0_0()
    {
        using var file = new TestScriptFile("[ScriptGroup('Tools')]\nparam()\n");

        ScriptVersionStamper.TryStamp(file.Path);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);
        Assert.Equal("1.0.0", manifest!.Version);
    }

    [Fact]
    public void Idempotent_LeavesAnAlreadyVersionedScriptUntouched()
    {
        const string original = "[ScriptVersion('2.3.4')]\nparam()\n";
        using var file = new TestScriptFile(original);

        var stamped = ScriptVersionStamper.TryStamp(file.Path);

        Assert.False(stamped);
        Assert.Equal(original, File.ReadAllText(file.Path));
    }

    [Fact]
    public void RunningTwice_StampsOnlyOnce()
    {
        using var file = new TestScriptFile("param()\n");

        Assert.True(ScriptVersionStamper.TryStamp(file.Path));
        Assert.False(ScriptVersionStamper.TryStamp(file.Path));

        var text = File.ReadAllText(file.Path);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, @"\[ScriptVersion\("));
    }

    [Fact]
    public void SkipsScript_WithNoParamBlock()
    {
        const string original = "[ScriptGroup('Tools')]\nWrite-Host 'no param here'\n";
        using var file = new TestScriptFile(original);

        var stamped = ScriptVersionStamper.TryStamp(file.Path);

        Assert.False(stamped);
        Assert.Equal(original, File.ReadAllText(file.Path));
    }

    [Fact]
    public void DoesNotMatch_ParamKeywordInsideAComment()
    {
        // "param(" only appears in a comment, never as a real block - nothing safe to attach to.
        const string original = "# this script has no real param( block\nWrite-Host 'x'\n";
        using var file = new TestScriptFile(original);

        Assert.False(ScriptVersionStamper.TryStamp(file.Path));
        Assert.Equal(original, File.ReadAllText(file.Path));
    }

    [Fact]
    public void PreservesCrlfNewlines()
    {
        using var file = new TestScriptFile("[ScriptGroup('Tools')]\r\nparam()\r\n");

        ScriptVersionStamper.TryStamp(file.Path);

        var text = File.ReadAllText(file.Path);
        Assert.Contains("[ScriptVersion('1.0.0')]\r\n", text);
        Assert.DoesNotContain("[ScriptVersion('1.0.0')]\n\n", text); // no stray lone-LF introduced
    }

    [Fact]
    public void PreservesLfNewlines()
    {
        using var file = new TestScriptFile("[ScriptGroup('Tools')]\nparam()\n");

        ScriptVersionStamper.TryStamp(file.Path);

        var text = File.ReadAllText(file.Path);
        Assert.Contains("[ScriptVersion('1.0.0')]\n", text);
        Assert.DoesNotContain("\r\n", text); // an LF file stays LF
    }
}

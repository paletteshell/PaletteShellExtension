using System.IO;
using System.Linq;
using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class PowerShellScriptParserTests
{
    // ----- Comment-based help -----------------------------------------------------------

    [Fact]
    public void Synopsis_BecomesTitle()
    {
        using var file = new TestScriptFile("""
            <#
            .SYNOPSIS
            My Cool Script
            #>
            Write-Output "hi"
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.NotNull(manifest);
        Assert.Equal("My Cool Script", manifest!.Title);
    }

    [Fact]
    public void MissingSynopsis_FallsBackToFileName()
    {
        using var file = new TestScriptFile("Write-Output \"hi\"");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.NotNull(manifest);
        Assert.Equal(Path.GetFileNameWithoutExtension(file.Path), manifest!.Title);
    }

    [Fact]
    public void Description_IsParsed()
    {
        using var file = new TestScriptFile("""
            <#
            .SYNOPSIS
            Title
            .DESCRIPTION
            Does a thing.
            #>
            Write-Output "hi"
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal("Does a thing.", manifest!.Description);
    }

    [Fact]
    public void ParameterHelp_BecomesLabel_WhenNoHelpMessage()
    {
        using var file = new TestScriptFile("""
            <#
            .SYNOPSIS
            Title
            .PARAMETER Name
            The name to greet.
            #>
            param(
                [string]$Name
            )
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        var parameter = Assert.Single(manifest!.Parameters);
        Assert.Equal("The name to greet.", parameter.Label);
    }

    // ----- Parameters ---------------------------------------------------------------------

    [Theory]
    [InlineData("[string]$P", "string")]
    [InlineData("[int]$P", "int")]
    [InlineData("[int32]$P", "int")]
    [InlineData("[long]$P", "int")]
    [InlineData("[double]$P", "number")]
    [InlineData("[decimal]$P", "number")]
    [InlineData("[switch]$P", "bool")]
    [InlineData("[bool]$P", "bool")]
    [InlineData("$P", "string")] // no type constraint at all
    public void ParameterType_MapsToUiType(string declaration, string expectedUiType)
    {
        using var file = new TestScriptFile($"param(\n    {declaration}\n)");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        var parameter = Assert.Single(manifest!.Parameters);
        Assert.Equal(expectedUiType, parameter.Type);
    }

    [Fact]
    public void ValidateSet_ProducesEnumWithOptions()
    {
        using var file = new TestScriptFile("""
            param(
                [ValidateSet('A','B','C')]
                [string]$Choice
            )
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        var parameter = Assert.Single(manifest!.Parameters);
        Assert.Equal("enum", parameter.Type);
        Assert.Equal(["A", "B", "C"], parameter.Options);
    }

    [Fact]
    public void ValidateRange_SetsMinAndMax()
    {
        using var file = new TestScriptFile("""
            param(
                [ValidateRange(1,10)]
                [int]$Count
            )
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        var parameter = Assert.Single(manifest!.Parameters);
        Assert.Equal(1, parameter.Min);
        Assert.Equal(10, parameter.Max);
    }

    [Theory]
    [InlineData("[Parameter(Mandatory=$true)]", true)]
    [InlineData("[Parameter(Mandatory)]", true)]
    [InlineData("[Parameter(Mandatory=$false)]", false)]
    [InlineData("", false)]
    public void Mandatory_SetsRequired(string attribute, bool expectRequired)
    {
        using var file = new TestScriptFile($"param(\n    {attribute}\n    [string]$P\n)");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        var parameter = Assert.Single(manifest!.Parameters);
        Assert.Equal(expectRequired ? true : (bool?)null, parameter.Required);
    }

    [Fact]
    public void HelpMessage_OverridesParameterHelpText()
    {
        using var file = new TestScriptFile("""
            <#
            .SYNOPSIS
            Title
            .PARAMETER Name
            Comment-based help text.
            #>
            param(
                [Parameter(HelpMessage='Explicit label')]
                [string]$Name
            )
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        var parameter = Assert.Single(manifest!.Parameters);
        Assert.Equal("Explicit label", parameter.Label);
    }

    [Fact]
    public void AllowExpression_SetsFlag()
    {
        using var file = new TestScriptFile("""
            param(
                [AllowExpression()]
                [string]$Expr
            )
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        var parameter = Assert.Single(manifest!.Parameters);
        Assert.True(parameter.AllowExpression);
    }

    [Theory]
    [InlineData("[int]$P = 5", 5)]
    [InlineData("[string]$P = \"hi\"", "hi")]
    [InlineData("[bool]$P = $true", true)]
    [InlineData("[bool]$P = $false", false)]
    public void DefaultValue_IsInterpretedByType(string declaration, object expected)
    {
        using var file = new TestScriptFile($"param(\n    {declaration}\n)");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        var parameter = Assert.Single(manifest!.Parameters);
        Assert.Equal(expected, parameter.Default);
    }

    [Fact]
    public void MultipleParameters_AllParsed()
    {
        using var file = new TestScriptFile("""
            param(
                [string]$Name,
                [int]$Count = 3,
                [switch]$Force
            )
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal(3, manifest!.Parameters.Count);
        Assert.Equal(["Name", "Count", "Force"], manifest.Parameters.Select(p => p.Name));
    }

    // ----- Script-level attributes ----------------------------------------------------------

    [Fact]
    public void ScriptHost_IsParsed()
    {
        using var file = new TestScriptFile("[ScriptHost('powershell')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal("powershell", manifest!.Host);
    }

    [Fact]
    public void ScriptCwd_IsParsed()
    {
        using var file = new TestScriptFile("[ScriptCwd('{ScriptDir}')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal("{ScriptDir}", manifest!.Cwd);
    }

    [Fact]
    public void RequiresElevationAttribute_SetsRequiresAdmin()
    {
        using var file = new TestScriptFile("[RequiresElevation()]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.True(manifest!.RequiresAdmin);
    }

    [Fact]
    public void RequiresAdminComment_SetsRequiresAdmin()
    {
        using var file = new TestScriptFile("#Requires -RunAsAdministrator\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.True(manifest!.RequiresAdmin);
    }

    [Theory]
    [InlineData("[ConfirmBeforeRun()]", "Are you sure you want to run this script?")]
    [InlineData("[ConfirmBeforeRun]", "Are you sure you want to run this script?")]
    [InlineData("[ConfirmBeforeRun('This deletes files')]", "This deletes files")]
    public void ConfirmBeforeRun_SetsMessage(string attribute, string expectedMessage)
    {
        using var file = new TestScriptFile($"{attribute}\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal(expectedMessage, manifest!.ConfirmMessage);
    }

    [Fact]
    public void ScriptTimeout_IsParsed()
    {
        using var file = new TestScriptFile("[ScriptTimeout(30000)]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal(30000, manifest!.TimeoutMs);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(999)]
    public void ScriptTimeout_BelowMinimum_IsIgnored(int timeoutMs)
    {
        using var file = new TestScriptFile($"[ScriptTimeout({timeoutMs})]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Null(manifest!.TimeoutMs);
    }

    [Fact]
    public void ScriptTimeout_AboveMaximum_IsClamped()
    {
        using var file = new TestScriptFile("[ScriptTimeout(999999999)]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal(600_000, manifest!.TimeoutMs);
    }

    [Fact]
    public void ScriptOutput_WithoutExtension_SetsOutputOnly()
    {
        using var file = new TestScriptFile("[ScriptOutput('Clipboard')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal("Clipboard", manifest!.Output);
        Assert.Null(manifest.FileExtension);
    }

    [Fact]
    public void ScriptOutput_WithExtensionHint_SplitsOutputAndExtension()
    {
        using var file = new TestScriptFile("[ScriptOutput('File:csv')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal("File", manifest!.Output);
        Assert.Equal("csv", manifest.FileExtension);
    }

    [Fact]
    public void ScriptGroup_IsParsed()
    {
        using var file = new TestScriptFile("[ScriptGroup('Utilities')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal("Utilities", manifest!.Group);
    }

    [Fact]
    public void ScriptEnv_MultipleAttributes_AllCaptured()
    {
        using var file = new TestScriptFile("""
            [ScriptEnv('KEY1','value1')]
            [ScriptEnv('KEY2','value2')]
            param()
            """);

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal("value1", manifest!.Env["KEY1"]);
        Assert.Equal("value2", manifest.Env["KEY2"]);
    }

    // ----- Icon glyph validation -------------------------------------------------------------
    //
    // Glyphs below are built from Unicode escapes rather than typed as literal characters:
    // Segoe MDL2/Fluent PUA codepoints and ZWJ emoji sequences are invisible or easily
    // mis-typed in an editor, which is exactly the failure mode this validation guards
    // against (see PowerShellScriptParser.IsValidIconGlyph).

    [Fact]
    public void ScriptIcon_SingleEmoji_IsAccepted()
    {
        var glyph = "\U0001F680"; // rocket
        using var file = new TestScriptFile($"[ScriptIcon('{glyph}')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal(glyph, manifest!.IconGlyph);
    }

    [Fact]
    public void ScriptIcon_SinglePuaGlyph_IsAccepted()
    {
        var glyph = "\uE8B7"; // Segoe MDL2 Assets glyph
        using var file = new TestScriptFile($"[ScriptIcon('{glyph}')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal(glyph, manifest!.IconGlyph);
    }

    [Fact]
    public void ScriptIcon_ZwjEmojiSequence_IsAcceptedAsSingleGrapheme()
    {
        // Family emoji: three codepoints joined by ZWJ (U+200D), rendering as one glyph.
        var glyph = "\U0001F468\u200D\U0001F469\u200D\U0001F467";
        using var file = new TestScriptFile($"[ScriptIcon('{glyph}')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Equal(glyph, manifest!.IconGlyph);
    }

    [Fact]
    public void ScriptIcon_MultiWordText_IsRejected()
    {
        using var file = new TestScriptFile("[ScriptIcon('todo icon please')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Null(manifest!.IconGlyph);
    }

    [Fact]
    public void ScriptIcon_Empty_IsNoOp()
    {
        using var file = new TestScriptFile("[ScriptIcon('')]\nparam()");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.Null(manifest!.IconGlyph);
    }

    // ----- Robustness ------------------------------------------------------------------------

    [Fact]
    public void NonexistentFile_ReturnsNull()
    {
        var manifest = PowerShellScriptParser.TryParseManifest(
            Path.Combine(Path.GetTempPath(), $"does-not-exist-{System.Guid.NewGuid():N}.ps1"));

        Assert.Null(manifest);
    }

    [Fact]
    public void NoParamBlock_ReturnsManifestWithNoParameters()
    {
        using var file = new TestScriptFile("Write-Output \"no params here\"");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.NotNull(manifest);
        Assert.Empty(manifest!.Parameters);
    }

    [Fact]
    public void UnbalancedParamBlock_DoesNotThrow_AndYieldsNoParameters()
    {
        using var file = new TestScriptFile("param(\n    [string]$Name");

        var manifest = PowerShellScriptParser.TryParseManifest(file.Path);

        Assert.NotNull(manifest);
        Assert.Empty(manifest!.Parameters);
    }

    // ----- Path token expansion ---------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExpandPathTokens_NullOrWhitespace_PassesThrough(string? input)
    {
        var result = PowerShellScriptParser.ExpandPathTokens(input, @"C:\scripts\test.ps1");

        Assert.Equal(input, result);
    }

    [Fact]
    public void ExpandPathTokens_ScriptDir_ExpandsToScriptDirectory()
    {
        var result = PowerShellScriptParser.ExpandPathTokens("{ScriptDir}\\out.txt", @"C:\scripts\test.ps1");

        Assert.Equal(@"C:\scripts\out.txt", result);
    }

    [Fact]
    public void ExpandPathTokens_Home_ExpandsToUserProfile()
    {
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

        var result = PowerShellScriptParser.ExpandPathTokens("{Home}\\notes.txt", @"C:\scripts\test.ps1");

        Assert.Equal(home + "\\notes.txt", result);
    }

    [Fact]
    public void ExpandPathTokens_Temp_ExpandsToTempPath()
    {
        var temp = Path.GetTempPath();

        var result = PowerShellScriptParser.ExpandPathTokens("{Temp}file.txt", @"C:\scripts\test.ps1");

        Assert.Equal(temp + "file.txt", result);
    }

    [Fact]
    public void ResolveCwd_NonexistentDirectory_ReturnsNull()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"does-not-exist-{System.Guid.NewGuid():N}");

        var result = PowerShellScriptParser.ResolveCwd(missing, @"C:\scripts\test.ps1");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveCwd_ExistingDirectory_ReturnsExpandedPath()
    {
        var temp = Path.GetTempPath();

        var result = PowerShellScriptParser.ResolveCwd("{Temp}", @"C:\scripts\test.ps1");

        Assert.Equal(temp, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveCwd_NullOrEmpty_PassesThrough(string? input)
    {
        var result = PowerShellScriptParser.ResolveCwd(input, @"C:\scripts\test.ps1");

        Assert.Equal(input, result);
    }

    [Fact]
    public void ResolveCwd_ScriptDirOfRealScript_Resolves()
    {
        using var file = new TestScriptFile("param()");
        var expectedDir = Path.GetDirectoryName(file.Path);

        var result = PowerShellScriptParser.ResolveCwd("{ScriptDir}", file.Path);

        Assert.Equal(expectedDir, result);
    }
}

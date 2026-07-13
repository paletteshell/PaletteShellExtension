using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class ScriptFailureFormTests
{
    [Fact]
    public void TemplateJson_ResultFailure_IncludesContextPreviewsAndActions()
    {
        var form = new ScriptFailureForm(
            @"C:\scripts\report.ps1",
            "pwsh",
            "-Token 'secret' -Name 'Ada'",
            new ScriptRunner.ScriptResult
            {
                ExitCode = 7,
                StandardError = "boom",
                StandardOutput = "partial output",
                TimedOut = true,
                DurationMs = 123,
            });

        var card = ParseCard(form);
        var text = card.ToJsonString();
        var facts = Facts(card);

        Assert.Contains("report timed out and was killed", text);
        Assert.Equal(ScriptRunner.DescribeShell("pwsh"), facts["Shell"]);
        Assert.Equal("-Token '***' -Name 'Ada'", facts["Args"]);
        Assert.Equal("7", facts["Exit code"]);
        Assert.Equal("Yes", facts["Timed out"]);
        Assert.Equal("123 ms", facts["Duration"]);
        Assert.Contains("stderr preview", text);
        Assert.Contains("boom", text);
        Assert.Contains("stdout preview", text);
        Assert.Contains("partial output", text);
        Assert.DoesNotContain("secret", text);
        AssertActions(card);
    }

    [Fact]
    public void TemplateJson_ResultFailure_OmitsEmptyStreamPreviews()
    {
        var form = new ScriptFailureForm(
            @"C:\scripts\report.ps1",
            "pwsh",
            "",
            new ScriptRunner.ScriptResult { ExitCode = 1 });

        var text = ParseCard(form).ToJsonString();

        Assert.DoesNotContain("stderr preview", text);
        Assert.DoesNotContain("stdout preview", text);
    }

    [Fact]
    public void TemplateJson_ExceptionFailure_IncludesExceptionContextAndActions()
    {
        var form = new ScriptFailureForm(
            @"C:\scripts\report.ps1",
            "powershell",
            "-ApiKey 'secret' -User 'Ada'",
            new InvalidOperationException("output side effect failed"));

        var card = ParseCard(form);
        var text = card.ToJsonString();
        var facts = Facts(card);

        Assert.Contains("report failed: output side effect failed", text);
        Assert.Equal(ScriptRunner.DescribeShell("powershell"), facts["Shell"]);
        Assert.Equal("-ApiKey '***' -User 'Ada'", facts["Args"]);
        Assert.Equal("InvalidOperationException", facts["Exception"]);
        Assert.Contains("exception preview", text);
        Assert.Contains("output side effect failed", text);
        Assert.DoesNotContain("secret", text);
        AssertActions(card);
    }

    private static JsonObject ParseCard(ScriptFailureForm form) =>
        JsonNode.Parse(form.TemplateJson)!.AsObject();

    private static Dictionary<string, string> Facts(JsonObject card)
    {
        var factSet = card["body"]!.AsArray()
            .Select(node => node!.AsObject())
            .First(node => string.Equals(node["type"]?.ToString(), "FactSet", StringComparison.Ordinal));

        return factSet["facts"]!.AsArray()
            .Select(node => node!.AsObject())
            .ToDictionary(
                node => node["title"]!.ToString(),
                node => node["value"]!.ToString(),
                StringComparer.Ordinal);
    }

    private static void AssertActions(JsonObject card)
    {
        var actions = card["actions"]!.AsArray()
            .Select(node => node!.AsObject())
            .ToDictionary(
                node => node["title"]!.ToString(),
                node => node["data"]!["verb"]!.ToString(),
                StringComparer.Ordinal);

        Assert.Equal("viewDetails", actions["View details"]);
        Assert.Equal("openLogs", actions["Open logs"]);
        Assert.Equal("openReports", actions["Open reports"]);
    }
}

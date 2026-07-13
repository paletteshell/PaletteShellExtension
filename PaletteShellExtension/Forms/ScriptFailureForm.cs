using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PaletteShellExtension.Forms;

/// <summary>
/// Small adaptive-card failure surface for content pages that finish after navigation. The card's
/// primary action opens the same common failure dialog used by command-returning run paths.
/// </summary>
internal sealed class ScriptFailureForm : FormContent
{
    private const int PreviewMaxChars = 700;

    private readonly string _scriptPath;
    private readonly string _host;
    private readonly string _args;
    private readonly ScriptRunner.ScriptResult? _result;
    private readonly Exception? _exception;

    public ScriptFailureForm(string scriptPath, string host, string args, ScriptRunner.ScriptResult? result)
    {
        _scriptPath = scriptPath;
        _host = host;
        _args = args;
        _result = result;

        TemplateJson = BuildTemplateJson(scriptPath, host, args, result);
        DataJson = """{"verb":"viewDetails"}""";
    }

    public ScriptFailureForm(string scriptPath, string host, string args, Exception exception)
    {
        _scriptPath = scriptPath;
        _host = host;
        _args = args;
        _exception = exception;

        TemplateJson = BuildTemplateJson(scriptPath, host, args, exception);
        DataJson = """{"verb":"viewDetails"}""";
    }

    public override CommandResult SubmitForm(string inputs, string data)
    {
        var verb = ParseVerb(data);
        if (string.Equals(verb, "openLogs", StringComparison.OrdinalIgnoreCase))
            return new OpenFolderCommand(Log.LogDirectory, "Open log folder").Invoke();

        if (string.Equals(verb, "openReports", StringComparison.OrdinalIgnoreCase))
            return new OpenFolderCommand(EditorLauncher.OutputDirectory, "Open report folder").Invoke();

        if (_exception is not null)
        {
            return ScriptFailurePresenter.OpenReportResult(_scriptPath, _host, _args, _exception);
        }

        return ScriptFailurePresenter.OpenReportResult(_scriptPath, _host, _args, _result);
    }

    private static string Description(ScriptRunner.ScriptResult? result) =>
        result is null ? "The script process could not be started." : ScriptRunner.DescribeFailure(result);

    private static string BuildTemplateJson(string scriptPath, string host, string args, ScriptRunner.ScriptResult? result)
    {
        var body = CreateBody(
            title: $"{Path.GetFileNameWithoutExtension(scriptPath)} {ScriptFailureReport.DescribeOutcome(result)}",
            description: Description(result),
            facts: CreateResultFacts(host, args, result));

        AddPreview(body, "stderr preview", result?.StandardError);
        AddPreview(body, "stdout preview", result?.StandardOutput);

        return CreateCard(body).ToJsonString();
    }

    private static string BuildTemplateJson(string scriptPath, string host, string args, Exception exception)
    {
        var body = CreateBody(
            title: $"{Path.GetFileNameWithoutExtension(scriptPath)} {ScriptFailureReport.DescribeOutcome(exception)}",
            description: exception.Message,
            facts:
            [
                Fact("Shell", ScriptRunner.DescribeShell(host)),
                Fact("Args", ArgsForDisplay(args)),
                Fact("Exception", exception.GetType().Name),
            ]);

        AddPreview(body, "exception preview", exception.ToString());

        return CreateCard(body).ToJsonString();
    }

    private static List<JsonNode?> CreateBody(string title, string description, IReadOnlyList<JsonObject> facts)
    {
        var body = new List<JsonNode?>
        {
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = title,
                ["weight"] = "Bolder",
                ["size"] = "Medium",
                ["wrap"] = true
            },
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = description,
                ["wrap"] = true,
                ["spacing"] = "Small"
            },
            new JsonObject
            {
                ["type"] = "FactSet",
                ["facts"] = new JsonArray(facts.Cast<JsonNode?>().ToArray())
            }
        };

        return body;
    }

    private static JsonObject CreateCard(IReadOnlyList<JsonNode?> body) =>
        new()
        {
            ["type"] = "AdaptiveCard",
            ["$schema"] = "https://adaptivecards.io/schemas/adaptive-card.json",
            ["version"] = "1.5",
            ["body"] = new JsonArray(body.ToArray()),
            ["actions"] = new JsonArray(
                SubmitAction("View details", "viewDetails"),
                SubmitAction("Open logs", "openLogs"),
                SubmitAction("Open reports", "openReports"))
        };

    private static List<JsonObject> CreateResultFacts(string host, string args, ScriptRunner.ScriptResult? result)
    {
        var facts = new List<JsonObject>
        {
            Fact("Shell", ScriptRunner.DescribeShell(host)),
            Fact("Args", ArgsForDisplay(args)),
        };

        if (result is null)
            return facts;

        facts.Add(Fact("Exit code", result.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        facts.Add(Fact("Timed out", result.TimedOut ? "Yes" : "No"));

        if (result.DurationMs is { } duration)
            facts.Add(Fact("Duration", $"{duration.ToString(System.Globalization.CultureInfo.InvariantCulture)} ms"));

        return facts;
    }

    private static JsonObject Fact(string title, string value) =>
        new()
        {
            ["title"] = title,
            ["value"] = value
        };

    private static JsonObject SubmitAction(string title, string verb) =>
        new()
        {
            ["type"] = "Action.Submit",
            ["title"] = title,
            ["data"] = new JsonObject { ["verb"] = verb }
        };

    private static void AddPreview(List<JsonNode?> body, string title, string? content)
    {
        var preview = Preview(content);
        if (string.IsNullOrWhiteSpace(preview))
            return;

        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = title,
            ["weight"] = "Bolder",
            ["spacing"] = "Medium",
            ["wrap"] = true
        });
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = preview,
            ["wrap"] = true,
            ["fontType"] = "Monospace",
            ["isSubtle"] = true,
            ["spacing"] = "Small",
            ["maxLines"] = 8
        });
    }

    private static string ArgsForDisplay(string args) =>
        string.IsNullOrWhiteSpace(args) ? "(none)" : ScriptFailureReport.RedactArgs(args);

    private static string? Preview(string? content)
    {
        var text = content?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length > PreviewMaxChars ? text[..PreviewMaxChars] + "..." : text;
    }

    private static string? ParseVerb(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;

        try
        {
            return JsonNode.Parse(data)?["verb"]?.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

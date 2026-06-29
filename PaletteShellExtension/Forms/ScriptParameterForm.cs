using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace PaletteShellExtension.Forms;

internal sealed class ScriptParameterForm : FormContent
{
    private readonly string _scriptPath;
    private readonly ScriptManifest _manifest;
    private readonly string _host;
    private readonly string? _cwd;
    private readonly Dictionary<string, string> _env;

    public ScriptParameterForm(
        string scriptPath,
        ScriptManifest manifest,
        string? host = null,
        string? cwd = null,
        Dictionary<string, string>? env = null)
    {
        
        _scriptPath = scriptPath;
        _manifest = manifest;
        _host = host ?? "pwsh";
        _cwd = cwd;
        _env = env ?? new(StringComparer.OrdinalIgnoreCase);

        TemplateJson = BuildTemplateJson();
        DataJson = BuildDataJson();
    }

    public override CommandResult SubmitForm(string payload)
    {
        try
        {
            var obj = JsonNode.Parse(payload)?.AsObject();
            if (obj is null) return CommandResult.Dismiss();

            var verb = obj["verb"]?.ToString();
            if (string.Equals(verb, "cancel", StringComparison.OrdinalIgnoreCase))
                return CommandResult.Dismiss();

            // Build argument list from form values
            var args = new List<string>();
            foreach (var param in _manifest.Parameters)
            {
                var value = obj[param.Name]?.ToString();

                // Skip empty optional parameters
                if (string.IsNullOrWhiteSpace(value) && param.Required != true)
                    continue;

                // Add parameter name and value
                if (param.Type == "bool")
                {
                    // For boolean parameters, pass $true or $false
                    var boolValue = value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                    args.Add($"-{param.Name}");
                    args.Add($"${boolValue.ToString().ToLowerInvariant()}");
                }
                else
                {
                    args.Add($"-{param.Name}");
                    args.Add(QuoteIfNeeded(value ?? ""));
                }
            }

            var argsLine = string.Join(" ", args);

            // Run script and wait for completion
            var timeout = _manifest.TimeoutMs is > 0 ? _manifest.TimeoutMs!.Value : 30000;
            var result = ScriptRunner.RunScriptAndWait(
                scriptPath: _scriptPath,
                args: argsLine,
                host: _host,
                cwd: _cwd,
                env: _env,
                requiresAdmin: false,
                timeoutMs: timeout);

            if (result == null)
                return CommandResult.ShowToast("Error: Failed to start script");

            if (result.TimedOut)
                return CommandResult.ShowToast("Script timed out");

            if (result.ExitCode != 0)
                return CommandResult.ShowToast($"Script failed with exit code {result.ExitCode}");

            // Return captured output or success message
            return !string.IsNullOrEmpty(result.StandardOutput)
                ? CommandResult.ShowToast(result.StandardOutput)
                : CommandResult.ShowToast("Script completed");
        }
        catch (Exception)
        {
            return CommandResult.GoBack();
        }
    }

    private string BuildTemplateJson()
    {
        // Build the body as a list of nodes, then materialize via the JsonArray(params)
        // constructor. (JsonArray.Add<T> is annotated RequiresUnreferencedCode for the
        // reflection path; we only ever hold primitives and JsonObject nodes here.)
        var body = new List<JsonNode?>
        {
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["size"] = "Medium",
                ["weight"] = "Bolder",
                ["text"] = _manifest.Title,
                ["wrap"] = true
            }
        };

        // Add description if present
        if (!string.IsNullOrWhiteSpace(_manifest.Description))
        {
            body.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["isSubtle"] = true,
                ["wrap"] = true,
                ["spacing"] = "Small",
                ["text"] = _manifest.Description
            });
        }

        // Add input elements for each parameter
        foreach (var param in _manifest.Parameters)
        {
            var inputElement = CreateInputElement(param);
            if (inputElement is not null)
                body.Add(inputElement);
        }

        var template = new JsonObject
        {
            ["schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.6",
            ["body"] = new JsonArray(body.ToArray()),
            ["actions"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "Action.Submit",
                    ["title"] = "Run",
                    ["data"] = new JsonObject { ["verb"] = "run" }
                },
                new JsonObject
                {
                    ["type"] = "Action.Submit",
                    ["title"] = "Cancel",
                    ["data"] = new JsonObject { ["verb"] = "cancel" }
                })
        };

        return template.ToJsonString();
    }

    private static JsonObject? CreateInputElement(ScriptParameter param)
    {
        switch (param.Type)
        {
            case "bool":
                return new JsonObject
                {
                    ["type"] = "Input.Toggle",
                    ["id"] = param.Name,
                    ["title"] = param.Label ?? param.Name,
                    ["value"] = param.Default?.ToString()?.ToLowerInvariant() ?? "false",
                    ["isRequired"] = param.Required ?? false
                };

            case "enum" when param.Options?.Count > 0:
                var choices = new List<JsonNode?>();
                foreach (var option in param.Options)
                    choices.Add(new JsonObject { ["title"] = option, ["value"] = option });

                return new JsonObject
                {
                    ["type"] = "Input.ChoiceSet",
                    ["id"] = param.Name,
                    ["label"] = param.Label ?? param.Name,
                    ["style"] = "compact",
                    ["value"] = param.Default?.ToString() ?? param.Options.First(),
                    ["isRequired"] = param.Required ?? false,
                    ["choices"] = new JsonArray(choices.ToArray())
                };

            case "int" or "number":
                JsonNode? min = param.Min is { } mn ? JsonValue.Create(mn) : null;
                JsonNode? max = param.Max is { } mx ? JsonValue.Create(mx) : null;
                return new JsonObject
                {
                    ["type"] = "Input.Number",
                    ["id"] = param.Name,
                    ["label"] = param.Label ?? param.Name,
                    ["placeholder"] = param.Placeholder ?? "",
                    ["value"] = ToJsonValue(param.Default),
                    ["min"] = min,
                    ["max"] = max,
                    ["isRequired"] = param.Required ?? false
                };

            default:
                return new JsonObject
                {
                    ["type"] = "Input.Text",
                    ["id"] = param.Name,
                    ["label"] = param.Label ?? param.Name,
                    ["placeholder"] = param.Placeholder ?? "",
                    ["value"] = param.Default?.ToString() ?? "",
                    ["isRequired"] = param.Required ?? false
                };
        }
    }

    private static JsonNode? ToJsonValue(object? value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        decimal m => JsonValue.Create(m),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(value.ToString())
    };

    private static string BuildDataJson() => new JsonObject().ToJsonString();

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\"\"";

        if (value.Contains(' ') || value.Contains('"'))
            return $"\"{value.Replace("\"", "`\"")}\"";

        return value;
    }
}

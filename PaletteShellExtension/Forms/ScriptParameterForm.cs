using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        var bodyElements = new List<object>
        {
            new
            {
                type = "TextBlock",
                size = "Medium",
                weight = "Bolder",
                text = _manifest.Title,
                wrap = true
            }
        };

        // Add description if present
        if (!string.IsNullOrWhiteSpace(_manifest.Description))
        {
            bodyElements.Add(new
            {
                type = "TextBlock",
                isSubtle = true,
                wrap = true,
                spacing = "Small",
                text = _manifest.Description
            });
        }

        // Add input elements for each parameter
        foreach (var param in _manifest.Parameters)
        {
            var inputElement = CreateInputElement(param);
            if (inputElement is not null)
                bodyElements.Add(inputElement);
        }

        var template = new
        {
            schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            type = "AdaptiveCard",
            version = "1.6",
            body = bodyElements.ToArray(),
            actions = new[]
            {
                new { type = "Action.Submit", title = "Run", data = new { verb = "run" } },
                new { type = "Action.Submit", title = "Cancel", data = new { verb = "cancel" } }
            }
        };

        return JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = false });
    }

    private object? CreateInputElement(ScriptParameter param)
    {
        return param.Type switch
        {
            "bool" => new
            {
                type = "Input.Toggle",
                id = param.Name,
                title = param.Label ?? param.Name,
                value = param.Default?.ToString()?.ToLowerInvariant() ?? "false",
                isRequired = param.Required ?? false
            },
            "enum" when param.Options?.Count > 0 => new
            {
                type = "Input.ChoiceSet",
                id = param.Name,
                label = param.Label ?? param.Name,
                style = "compact",
                value = param.Default?.ToString() ?? param.Options.First(),
                isRequired = param.Required ?? false,
                choices = param.Options.Select(o => new { title = o, value = o }).ToArray()
            },
            "int" or "number" => new
            {
                type = "Input.Number",
                id = param.Name,
                label = param.Label ?? param.Name,
                placeholder = param.Placeholder ?? "",
                value = param.Default,
                min = param.Min,
                max = param.Max,
                isRequired = param.Required ?? false
            },
            _ => new
            {
                type = "Input.Text",
                id = param.Name,
                label = param.Label ?? param.Name,
                placeholder = param.Placeholder ?? "",
                value = param.Default?.ToString() ?? "",
                isRequired = param.Required ?? false
            }
        };
    }

    private string BuildDataJson()
    {
        var data = new { };
        return JsonSerializer.Serialize(data);
    }

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\"\"";

        if (value.Contains(' ') || value.Contains('"'))
            return $"\"{value.Replace("\"", "`\"")}\"";

        return value;
    }
}

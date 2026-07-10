using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PaletteShellExtension.Forms;

internal sealed class ScriptParameterForm : FormContent
{
    private readonly string _scriptPath;
    private readonly ScriptManifest _manifest;
    private readonly ScriptExecutionPlan _plan;
    private readonly Action<string>? _onMarkdown;
    private readonly Action? _onRunStarted;
    private readonly Action? _onRunFinished;

    public ScriptParameterForm(
        string scriptPath,
        ScriptManifest manifest,
        ScriptExecutionPlan plan,
        Action<string>? onMarkdown = null,
        Action? onRunStarted = null,
        Action? onRunFinished = null)
    {

        _scriptPath = scriptPath;
        _manifest = manifest;
        _plan = plan;
        _onMarkdown = onMarkdown;
        _onRunStarted = onRunStarted;
        _onRunFinished = onRunFinished;

        TemplateJson = BuildTemplateJson();
        DataJson = BuildDataJson();
    }

    public override CommandResult SubmitForm(string inputs, string data)
    {
        try
        {
            // The clicked button's verb is carried in the action's `data`, not in the
            // input values, so it must be read from the `data` argument.
            var verb = ParseVerb(data);
            if (string.Equals(verb, "cancel", StringComparison.OrdinalIgnoreCase))
                return CommandResult.Dismiss();

            var obj = JsonNode.Parse(inputs)?.AsObject();
            if (obj is null) return CommandResult.Dismiss();

            // The Adaptive Card's `isRequired` flag is enforced client-side by the host, but
            // that isn't guaranteed for every input type or host version — this is the
            // server-side backstop so a mandatory parameter can never reach the script empty.
            var missing = GetMissingRequiredFields(_manifest, obj);

            if (missing.Count > 0)
            {
                return CommandResult.ShowToast(new ToastArgs
                {
                    Message = $"Required: {string.Join(", ", missing)}",
                    Result = CommandResult.KeepOpen()
                });
            }

            // Build argument line from form values (quoting centralized in ScriptArgumentBuilder).
            var argsLine = ScriptArgumentBuilder.BuildFromForm(_manifest.Parameters, obj);

            // Every waited run happens on a background thread and renders its result (or performs
            // its clipboard/open/file effect) in place, showing a "Running…" spinner meanwhile — so
            // a slow script no longer freezes the form while the host is blocked on the submit COM
            // call. Elevated runs can't capture output, so they launch fire-and-forget and report
            // completion immediately.
            Func<CommandResult> run = _plan.RequiresAdmin
                ? () => ExecuteElevated(argsLine)
                : () => StartAsyncRun(argsLine);

            // Destructive scripts gate behind a confirmation dialog; only the dialog's
            // primary command runs the script (with the values already collected here).
            if (!string.IsNullOrWhiteSpace(_manifest.ConfirmMessage))
            {
                var scriptName = System.IO.Path.GetFileNameWithoutExtension(_scriptPath);
                return CommandResult.Confirm(new ConfirmationArgs
                {
                    Title = $"Run {scriptName}?",
                    Description = _manifest.ConfirmMessage,
                    PrimaryCommand = new CallbackCommand($"Run {scriptName}", run),
                    IsPrimaryCommandCritical = true,
                });
            }

            return run();
        }
        catch (Exception)
        {
            return CommandResult.GoBack();
        }
    }

    /// <summary>Runs the script on a background thread so the host's submit COM call returns at
    /// once, showing a "Running…" spinner meanwhile and rendering the result in place when it
    /// finishes. On success the declared output effect (clipboard/open/file) is performed and a
    /// short status is shown; Markdown/Toast modes render their output. Never reached for elevated
    /// runs (they can't capture output — see <see cref="ExecuteElevated"/>).</summary>
    private CommandResult StartAsyncRun(string argsLine)
    {
        _onRunStarted?.Invoke();

        _ = Task.Run(async () =>
        {
            string body;
            try
            {
                var result = await ScriptExecutionService.RunAsync(_plan, argsLine);
                body = FormatAsyncResult(result);
            }
            catch (Exception ex)
            {
                body = $"**Error running script**\n\n```\n{ex.Message}\n```";
            }

            try
            {
                _onMarkdown?.Invoke(body);
            }
            finally
            {
                _onRunFinished?.Invoke();
            }
        });

        return CommandResult.KeepOpen();
    }

    /// <summary>Turns a completed run into the Markdown body to render in place. Failures render
    /// inline (with stderr); a success performs the declared output effect via
    /// <see cref="ScriptRunDispatcher"/> and shows its status (or the rendered output for
    /// Markdown/Toast modes).</summary>
    private string FormatAsyncResult(ScriptRunner.ScriptResult? result)
    {
        if (result is null)
            return "_Failed to start script._";

        if (result.TimedOut)
            return "_Script timed out._";

        if (result.ExitCode != 0)
        {
            var error = result.StandardError?.Trim();
            return string.IsNullOrEmpty(error)
                ? $"**Script failed with exit code {result.ExitCode}.**"
                : $"**Script failed with exit code {result.ExitCode}.**\n\n```\n{error}\n```";
        }

        var scriptName = System.IO.Path.GetFileNameWithoutExtension(_scriptPath);
        return ScriptRunDispatcher.Apply(_manifest, result.StandardOutput, scriptName).Status;
    }

    /// <summary>Launches an elevated script fire-and-forget. Elevated scripts can't have their
    /// output captured, so the routing gate only lets one reach here when its output is None —
    /// there's nothing to wait for or surface, so this returns immediately.</summary>
    private CommandResult ExecuteElevated(string argsLine)
    {
        var started = ScriptExecutionService.RunFireAndForget(_plan, argsLine);
        return started
            ? CommandResult.ShowToast("Script completed")
            : ScriptFailurePresenter.ToCommandResult(_scriptPath, _plan.Host, argsLine, null);
    }

    /// <summary>Returns the label/name of each required parameter whose submitted value is
    /// empty or whitespace-only.</summary>
    internal static List<string> GetMissingRequiredFields(ScriptManifest manifest, JsonObject values) =>
        manifest.Parameters
            .Where(p => p.Required == true && string.IsNullOrWhiteSpace(values[p.Name]?.ToString()))
            .Select(p => p.Label ?? p.Name)
            .ToList();

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
            case "switch":
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

    private static JsonValue? ToJsonValue(object? value) => value switch
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


    private static string? ParseVerb(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;
        try
        {
            return JsonNode.Parse(data)?["verb"]?.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

}

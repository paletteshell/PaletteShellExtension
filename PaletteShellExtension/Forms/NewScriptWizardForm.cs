using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace PaletteShellExtension.Forms;
internal sealed partial class NewScriptWizardForm : FormContent
{
    private readonly string _root;

    public NewScriptWizardForm(string root)
    {
        TemplateJson = """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "size": "Medium", "weight": "Bolder", "text": "Create new script", "wrap": true },
    {
      "type": "Input.Text",
      "id": "name",
      "label": "File name",
      "placeholder": "MyScript",
      "value": "MyScript"
    },
    {
      "type": "Input.Text",
      "id": "description",
      "label": "Description (optional)",
      "placeholder": "Describe what this script does"
    },
    {
      "type": "Input.Text",
      "id": "group",
      "label": "Group",
      "value": "General"
    },
    {
      "type": "Input.Text",
      "id": "icon",
      "label": "Icon (emoji or glyph)",
      "value": "🧩"
    },
    {
      "type": "Input.ChoiceSet",
      "id": "output",
      "label": "Output mode",
      "style": "compact",
      "value": "None",
      "choices": [
        { "title": "None — run silently", "value": "None" },
        { "title": "Toast — show output in a notification", "value": "Toast" },
        { "title": "Clipboard — copy output", "value": "Clipboard" },
        { "title": "Markdown — render output as Markdown", "value": "Markdown" },
        { "title": "Result — single copyable result", "value": "Result" },
        { "title": "List — searchable list of items", "value": "List" },
        { "title": "File — open output in editor", "value": "File" }
      ]
    },
    {
      "type": "Input.ChoiceSet",
      "id": "host",
      "label": "Host",
      "style": "compact",
      "value": "pwsh",
      "choices": [
        { "title": "PowerShell 7 (pwsh)", "value": "pwsh" },
        { "title": "Windows PowerShell 5.1", "value": "powershell" }
      ]
    },
    {
      "type": "Input.Number",
      "id": "timeout",
      "label": "Timeout (ms)",
      "value": 20000,
      "min": 1000,
      "max": 600000
    },
    {
      "type": "Input.Toggle",
      "id": "elevate",
      "title": "Requires administrator rights",
      "valueOn": "true",
      "valueOff": "false",
      "value": "false"
    },
    {
      "type": "Input.Text",
      "id": "confirm",
      "label": "Confirmation message (optional — prompts before running)",
      "placeholder": "Are you sure you want to run this script?"
    },
    {
      "type": "Input.Toggle",
      "id": "open",
      "title": "Open after create",
      "valueOn": "true",
      "valueOff": "false",
      "value": "true"
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Create", "data": { "verb": "create" } },
    { "type": "Action.Submit", "title": "Cancel", "data": { "verb": "cancel" } }
  ]
}
""";
        _root = root;
    }


    public override CommandResult SubmitForm(string inputs, string data)
    {
        // The clicked button's verb is carried in the action's `data`, not in the
        // input values, so it must be read from the `data` argument.
        var verb = ParseVerb(data);
        if (string.Equals(verb, "cancel", StringComparison.OrdinalIgnoreCase))
            return CommandResult.GoBack();

        var formInput = JsonNode.Parse(inputs)?.AsObject();
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        var rawName = formInput["name"]?.ToString()?.Trim();
        var name = string.IsNullOrWhiteSpace(rawName) ? "MyScript" : rawName;

        var options = new ScriptOptions(
            Description: formInput["description"]?.ToString()?.Trim(),
            Group: formInput["group"]?.ToString()?.Trim(),
            Icon: formInput["icon"]?.ToString()?.Trim(),
            Output: formInput["output"]?.ToString()?.Trim(),
            Host: formInput["host"]?.ToString()?.Trim(),
            TimeoutMs: ParseTimeout(formInput["timeout"]?.ToString()),
            RequiresElevation: (formInput["elevate"]?.ToString() ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            ConfirmMessage: formInput["confirm"]?.ToString()?.Trim());

        var open = (formInput["open"]?.ToString() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        var path = CreateScript(_root, name, options);

        if (open && path is not null)
            EditorLauncher.Open(path);

        return CommandResult.GoBack();
    }

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

    private static int ParseTimeout(string? raw)
    {
        const int Default = 20000, Min = 1000, Max = 600_000;
        if (!int.TryParse(raw, out var value) || value < Min)
            return Default;
        return Math.Min(value, Max);
    }

    /// <summary>Collected wizard answers that shape the generated attribute block and body.</summary>
    private readonly record struct ScriptOptions(
        string? Description,
        string? Group,
        string? Icon,
        string? Output,
        string? Host,
        int TimeoutMs,
        bool RequiresElevation,
        string? ConfirmMessage);

    private static string? CreateScript(string root, string rawName, ScriptOptions options)
    {
        Directory.CreateDirectory(root);

        var safe = Sanitize(rawName);
        if (!safe.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            safe += ".ps1";

        var full = UniquePath(Path.Combine(root, safe));
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var content = BuildHeader(Path.GetFileNameWithoutExtension(full), options) + BuildParamBlock() + BuildBody(options.Output);
        File.WriteAllText(full, content, utf8NoBom);
        return full;
    }
    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name) sb.Append(invalid.Contains(ch) ? '_' : ch);
        var s = sb.ToString().Trim();
        return s.Length == 0 ? "Script" : s;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    // ---------- templates (match the sample-script format the parser reads:
    //            comment-based help + [Script*] attributes) ----------

    private static string EscapeSingleQuoted(string value) => value.Replace("'", "''");

    private static string BuildHeader(string name, ScriptOptions options)
    {
        var sb = new StringBuilder();
        sb.Append("using module .\\PaletteScriptAttributes.psm1\n\n");
        sb.Append("<#\n.SYNOPSIS\n    ").Append(name).Append('\n');

        var description = string.IsNullOrWhiteSpace(options.Description)
            ? "Describe what this script does."
            : options.Description;
        sb.Append(".DESCRIPTION\n    ").Append(description).Append('\n');
        sb.Append("#>\n");

        var host = string.IsNullOrWhiteSpace(options.Host) ? "pwsh" : options.Host;
        sb.Append(CultureInfo.InvariantCulture, $"[ScriptHost('{host}')]\n");

        var group = string.IsNullOrWhiteSpace(options.Group) ? "General" : options.Group;
        sb.Append(CultureInfo.InvariantCulture, $"[ScriptGroup('{EscapeSingleQuoted(group)}')]\n");

        if (!string.IsNullOrWhiteSpace(options.Icon))
            sb.Append(CultureInfo.InvariantCulture, $"[ScriptIcon('{EscapeSingleQuoted(options.Icon)}')]\n");

        if (options.RequiresElevation)
            sb.Append("[RequiresElevation()]\n");

        if (!string.IsNullOrWhiteSpace(options.ConfirmMessage))
            sb.Append(CultureInfo.InvariantCulture, $"[ConfirmBeforeRun('{EscapeSingleQuoted(options.ConfirmMessage)}')]\n");

        sb.Append(CultureInfo.InvariantCulture, $"[ScriptTimeout({options.TimeoutMs})]\n");

        var output = string.IsNullOrWhiteSpace(options.Output) ? "None" : options.Output;
        sb.Append(CultureInfo.InvariantCulture, $"[ScriptOutput('{output}')]\n");

        sb.Append("[CmdletBinding()]\n");
        return sb.ToString();
    }

    private static string BuildParamBlock() =>
@"param(
    # Add parameters here
    # [Parameter(Mandatory=$true)]
    # [string]$Path
)

";

    /// <summary>A short, working body matching the chosen output mode, so the scaffold
    /// demonstrates the right shape (e.g. Result mode expects a single emitted value)
    /// instead of leaving every mode with the same generic placeholder.</summary>
    private static string BuildBody(string? output) => (string.IsNullOrWhiteSpace(output) ? "None" : output) switch
    {
        "Result" =>
            "# Emit just the value; Result mode shows it as a single copyable result.\n" +
            "[System.Guid]::NewGuid().ToString()\n",

        "List" =>
            "# Print newline-delimited items, or a JSON array of objects (title/subtitle/value/url/icon) for richer items.\n" +
            "Get-ChildItem -Name\n",

        "Markdown" =>
            "# Captured stdout is rendered as Markdown on its own page.\n" +
            "\"## Report`n`nSomething happened.\"\n",

        "Clipboard" =>
            "# Whatever stdout writes is copied to the clipboard.\n" +
            "\"Copied text\"\n",

        "File" =>
            "# Captured stdout is written to a temp file and opened in your editor.\n" +
            "Get-Process | Select-Object Name, Id, CPU | ConvertTo-Csv -NoTypeInformation\n",

        "Toast" =>
            "# Captured stdout is shown in a notification.\n" +
            "\"Done!\"\n",

        _ =>
            "# --- Script body ---\n" +
            "# Write-Host \"Hello from PaletteShell!\"\n"
    };
}

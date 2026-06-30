using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
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
        var open = (formInput["open"]?.ToString() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        var name = string.IsNullOrWhiteSpace(rawName) ? "MyScript" : rawName;
        var path = CreateScript(_root, name);

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

    private static string? CreateScript(string root, string rawName)
    {
        Directory.CreateDirectory(root);

        var safe = Sanitize(rawName);
        if (!safe.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            safe += ".ps1";

        var full = UniquePath(Path.Combine(root, safe));
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(full, DefaultHeader(Path.GetFileNameWithoutExtension(full)) + BlankBody(), utf8NoBom);
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

    private static string DefaultHeader(string name) =>
$@"using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    {name}
.DESCRIPTION
    Describe what this script does.
#>
[ScriptHost('pwsh')]
[ScriptGroup('General')]
[ScriptIcon('🧩')]
[ScriptTimeout(20000)]
[ScriptOutput('None')]
[CmdletBinding()]
";

    private static string BlankBody() =>
@"param(
    # Add parameters here
    # [Parameter(Mandatory=$true)]
    # [string]$Path
)

# --- Script body ---
# Write-Host ""Hello from PaletteShell!""
";

}

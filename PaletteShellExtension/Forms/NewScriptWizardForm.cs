using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PaletteShellExtension.Forms;
internal sealed partial class NewScriptWizardForm : FormContent
{
    private readonly string _root;

    public NewScriptWizardForm(string root)
    {
        TemplateJson = $$"""
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
      "type": "Input.ChoiceSet",
      "id": "template",
      "label": "Template",
      "style": "compact",
      "value": "blank",
      "choices": [
        { "$data": "${templates}", "title": "${title}", "value": "${value}" }
      ]
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

        DataJson = $$"""
{
  "templates": [
    { "title": "Blank script", "value": "blank" },
  ]
}
""";
        _root = root;
    }


    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload)?.AsObject();
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        var verb = formInput["verb"]?.ToString();
        if (string.Equals(verb, "cancel", StringComparison.OrdinalIgnoreCase))
            return CommandResult.GoBack();

        var rawName = formInput["name"]?.ToString()?.Trim();
        var template = formInput["template"]?.ToString() ?? "blank";
        var open = (formInput["open"]?.ToString() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        var name = string.IsNullOrWhiteSpace(rawName) ? "MyScript" : rawName;
        var path = CreateScript(_root, name, template);

        if (open && path is not null)
            OpenInEditor(path);

        return CommandResult.GoBack();
    }

    private static string? CreateScript(string root, string rawName, string templateKey)
    {
        Directory.CreateDirectory(root);

        var safe = Sanitize(rawName);
        if (!safe.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            safe += ".ps1";

        var full = UniquePath(Path.Combine(root, safe));
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(full, BuildTemplate(full, templateKey), utf8NoBom);
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
    private static string BuildTemplate(string path, string key)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return key switch
        {
            "clipboard-csv" => ClipboardCsvHeader(fileName) + ClipboardCsvBody(),
            "index-lines" => IndexLinesHeader(fileName) + IndexLinesBody(),
            _ => DefaultHeader(fileName) + BlankBody()
        };
    }


    private static void OpenInEditor(string file)
    {
        var editor = Environment.GetEnvironmentVariable("VISUAL")
                 ?? Environment.GetEnvironmentVariable("EDITOR")
                 ?? "notepad.exe";
        Process.Start(new ProcessStartInfo(editor, $"\"{file}\"") { UseShellExecute = true });
    }

    // ---------- templates (headers match your extended ScriptMetadata) ----------



    private static string DefaultHeader(string name) =>
$@"<#
Title: {name}
Description: Describe what this script does.
Args:
Group: General
Tags: sample
Icon: 🧩
IconGlyph: \uE756
RequiresAdmin: false
TimeoutMs: 20000
Env:
Host: pwsh
Cwd: {{ScriptDir}}
Mutex:
Output: None
OutputAction: None
#>
";

    private static string BlankBody() =>
@"
param(
    # Add parameters here
    # [Parameter(Mandatory=$true)]
    # [string]$Path
)

# --- Script body ---
# Write-Host ""Hello from PaletteShell!""
";

    private static string ClipboardCsvHeader(string name) =>
$@"using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Clipboard → CSV (quoted)
.DESCRIPTION
    Convert multiline clipboard text to comma-delimited, quote-wrapped CSV and copy back.
#>
[ScriptHost('pwsh')]
[ScriptGroup('Clipboard')]
[ScriptIcon('🧾')]
[ScriptTimeout(15000)]
[ScriptOutput('None')]
[CmdletBinding()]
";

    private static string ClipboardCsvBody() =>
@"
param()

$text  = Get-ClipboardText
$lines = $text -split ""(\r\n|\n|\r)"" | Where-Object { $_ -ne """" }
$csv   = ($lines | ForEach-Object { '""' + ($_ -replace '""','""""') + '""' }) -join ','
Set-ClipboardText $csv
Write-Host ""Converted $($lines.Count) lines to CSV and copied to clipboard""
";

    private static string IndexLinesHeader(string name) =>
$@"using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Add index to lines in file
.DESCRIPTION
    Writes <file>.indexed.txt next to the original.
.PARAMETER Path
    The file to index
#>
[ScriptHost('pwsh')]
[ScriptCwd('{{ScriptDir}}')]
[ScriptGroup('Files')]
[ScriptIcon('🔢')]
[ScriptTimeout(30000)]
[ScriptOutput('None')]
[CmdletBinding()]
";

    private static string IndexLinesBody() =>
@"
param(
    [Parameter(Mandatory=$true, HelpMessage=""File to index"")]
    [string]$Path
)

$lines   = Get-Content -LiteralPath $Path
$indexed = for ($i=0; $i -lt $lines.Count; $i++) { ""{0:D4}: {1}"" -f ($i+1), $lines[$i] }
$out     = [System.IO.Path]::ChangeExtension($Path, "".indexed.txt"")
$indexed | Set-Content -LiteralPath $out -NoNewline:$false
Write-Host ""Wrote $out""
";
}


using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
using System.IO;
using System.Text.Json.Nodes;

namespace PaletteShellExtension.Forms;

/// <summary>
/// First-run (and re-run, from "Change scripts folder") prompt that collects the folder
/// PaletteShell should scan for <c>.ps1</c> scripts. Modeled on <see cref="NewScriptWizardForm"/>.
/// </summary>
internal sealed partial class ScriptsFolderSetupForm : FormContent
{
    private readonly string _suggestedFolder;
    private readonly Action<string> _onConfigured;

    public ScriptsFolderSetupForm(string suggestedFolder, Action<string> onConfigured)
    {
        _suggestedFolder = suggestedFolder;
        _onConfigured = onConfigured;

        var escaped = EscapeJson(suggestedFolder);
        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "size": "Medium", "weight": "Bolder", "text": "Choose your scripts folder", "wrap": true },
    { "type": "TextBlock", "text": "PaletteShell reads .ps1 scripts from this folder and creates it if it doesn't exist yet — pick a synced folder if you use PaletteShell on more than one machine.", "wrap": true, "isSubtle": true },
    {
      "type": "Input.Text",
      "id": "folder",
      "label": "Folder path",
      "value": "{{escaped}}",
      "placeholder": "{{escaped}}"
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Use this folder", "data": { "verb": "use" } }
  ]
}
""";
    }

    public override CommandResult SubmitForm(string inputs, string data)
    {
        var formInput = JsonNode.Parse(inputs)?.AsObject();
        var raw = formInput?["folder"]?.ToString()?.Trim();
        var folder = string.IsNullOrWhiteSpace(raw)
            ? _suggestedFolder
            : Environment.ExpandEnvironmentVariables(raw);

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't create scripts folder '{folder}': {ex.Message}");
            return CommandResult.ShowToast($"Couldn't use that folder: {ex.Message}");
        }

        PaletteShellSettingsManager.Instance.ScriptsFolder = folder;
        _onConfigured(folder);

        return CommandResult.GoBack();
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

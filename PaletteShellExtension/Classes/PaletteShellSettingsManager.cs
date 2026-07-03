using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Owns PaletteShell's extension-level settings (surfaced by the host as the provider's
/// settings page) and persists them to a JSON file under the extension's app-data folder.
/// </summary>
internal sealed class PaletteShellSettingsManager : JsonSettingsManager
{
    public const string HostAuto = "auto";
    public const string HostPwsh = "pwsh";
    public const string HostPowerShell = "powershell";

    // Used when a script must be waited on to honor its output mode but declared no
    // [ScriptTimeout], and the user hasn't overridden the default (or entered garbage).
    private const int FallbackTimeoutMs = 30000;

    public static PaletteShellSettingsManager Instance { get; } = new();

    private readonly ChoiceSetSetting _defaultHost = new(
        "defaultHost",
        "Default script host",
        "Used for scripts that don't declare [ScriptHost(...)].",
        new List<ChoiceSetSetting.Choice>
        {
            new("Auto (recommended)", HostAuto),
            new("PowerShell 7 (pwsh)", HostPwsh),
            new("Windows PowerShell 5.1", HostPowerShell),
        });

    private readonly TextSetting _defaultTimeoutMs = new(
        "defaultTimeoutMs",
        "Default timeout (ms)",
        "Used for scripts that don't declare [ScriptTimeout(...)].",
        FallbackTimeoutMs.ToString(CultureInfo.InvariantCulture));

    private readonly TextSetting _preferredEditor = new(
        "preferredEditor",
        "Preferred editor",
        "Command or path used by \"Open in editor\". Leave blank to use $VISUAL, then $EDITOR, then Notepad.",
        string.Empty);

    private readonly TextSetting _scriptsFolder = new(
        "scriptsFolder",
        "Scripts folder",
        "Folder PaletteShell scans for .ps1 scripts. Changing this doesn't move existing scripts or pins — run \"Reload scripts\" afterward to pick it up.",
        string.Empty);

    /// <summary>The configured default host: <c>"auto"</c>, <c>"pwsh"</c>, or <c>"powershell"</c>.</summary>
    public string DefaultHost => _defaultHost.Value ?? HostAuto;

    public int DefaultTimeoutMs =>
        int.TryParse(_defaultTimeoutMs.Value, out var ms) && ms > 0 ? ms : FallbackTimeoutMs;

    /// <summary>The user's preferred editor command/path, or null when unset (fall back to env vars/Notepad).</summary>
    public string? PreferredEditor =>
        string.IsNullOrWhiteSpace(_preferredEditor.Value) ? null : _preferredEditor.Value.Trim();

    /// <summary>The configured scripts folder, or null when the user hasn't chosen one yet.</summary>
    public string? ScriptsFolder
    {
        get => string.IsNullOrWhiteSpace(_scriptsFolder.Value) ? null : _scriptsFolder.Value.Trim();
        set
        {
            _scriptsFolder.Value = value ?? string.Empty;
            SaveSettings();
        }
    }

    private PaletteShellSettingsManager()
    {
        var settingsDir = Utilities.BaseSettingsPath("PaletteShellExtension");
        Directory.CreateDirectory(settingsDir);
        FilePath = Path.Combine(settingsDir, "settings.json");

        Settings.Add(_defaultHost);
        Settings.Add(_defaultTimeoutMs);
        Settings.Add(_preferredEditor);
        Settings.Add(_scriptsFolder);

        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}

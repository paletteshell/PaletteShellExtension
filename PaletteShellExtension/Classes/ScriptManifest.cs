using System;
using System.Collections.Generic;

namespace PaletteShellExtension.Classes;

public sealed class ScriptManifest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public List<ScriptParameter> Parameters { get; set; } = [];

    public string? Group { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? IconGlyph { get; set; }
    public string? Version { get; set; }

    // Minimum PaletteShell app version (SemVer) required to run this script. A script that
    // omits [RequiresPaletteShellMinimum(...)] defaults to "0.0.6" (see PowerShellScriptParser)
    // rather than staying null, so this is only null when parsing raw content that skips that
    // default (e.g. via ParseScriptAttributes directly in a test).
    public string? MinVersion { get; set; }

    // Maximum PaletteShell app version (SemVer) this script still works on, from
    // [RequiresPaletteShellMaximum(...)]. Unlike MinVersion, unset means "no ceiling" - most
    // scripts don't rely on behavior later removed, so null (rather than a defaulted baseline)
    // is the correct default here.
    public string? MaxVersion { get; set; }

    public bool? RequiresAdmin { get; set; }

    // When set, running the script first prompts a confirmation dialog carrying this
    // message. Pairs with RequiresAdmin to gate destructive scripts.
    public string? ConfirmMessage { get; set; }

    public int? TimeoutMs { get; set; }
    public Dictionary<string, string> Env { get; set; } = [];

    public string? Host { get; set; }
    public string? Cwd { get; set; }

    public string Output { get; set; } = "None";

    // Extension hint for File output mode (e.g. ".json"), so the temp file opens
    // with the right syntax highlighting. Ignored by other output modes.
    public string? FileExtension { get; set; }
}

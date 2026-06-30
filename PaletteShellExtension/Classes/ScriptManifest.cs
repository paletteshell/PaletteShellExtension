using System;
using System.Collections.Generic;

namespace PaletteShellExtension.Classes;

public sealed class ScriptManifest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public List<ScriptParameter> Parameters { get; set; } = [];

    public string? Group { get; set; }
    public string? IconGlyph { get; set; }

    public bool? RequiresAdmin { get; set; }
    public int? TimeoutMs { get; set; }
    public Dictionary<string, string> Env { get; set; } = [];

    public string? Host { get; set; }
    public string? Cwd { get; set; }

    public string Output { get; set; } = "None";
}

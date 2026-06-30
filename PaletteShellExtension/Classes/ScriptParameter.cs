using System.Collections.Generic;

namespace PaletteShellExtension.Classes;

public sealed class ScriptParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string"; // string,int,number,bool,enum,file,folder
    public string? Label { get; set; }
    public object? Default { get; set; }
    public bool? Required { get; set; }
    public string? Placeholder { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public List<string>? Options { get; set; } // for enum

    // When true (script marked the parameter [AllowExpression]), the form value is
    // injected verbatim for PowerShell to evaluate instead of being passed literally.
    public bool AllowExpression { get; set; }
}

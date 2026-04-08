using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using PaletteShellExtension.Classes;

namespace PaletteShellExtension;

internal static class PowerShellScriptParser
{
    public static ScriptManifest? TryParseManifest(string ps1Path)
    {
        if (!File.Exists(ps1Path))
            return null;

        try
        {
            var scriptContent = File.ReadAllText(ps1Path, Encoding.UTF8);
            var ast = Parser.ParseInput(scriptContent, out _, out var errors);

            // Only proceed if there are no critical parsing errors
            if (errors.Any(e => !e.IncompleteInput))
                return null;

            var paramBlock = ast.Find(a => a is ParamBlockAst, true) as ParamBlockAst;
            var helpContent = ast.GetHelpContent();

            var manifest = new ScriptManifest
            {
                Title = helpContent?.Synopsis ?? Path.GetFileNameWithoutExtension(ps1Path),
                Description = helpContent?.Description,
                Parameters = []
            };

            // Parse script-level attributes (metadata)
            var scriptBlockAst = ast.Find(a => a is ScriptBlockAst, true) as ScriptBlockAst;
            if (scriptBlockAst?.ParamBlock?.Attributes is not null)
            {
                foreach (var attr in scriptBlockAst.ParamBlock.Attributes.OfType<AttributeAst>())
                {
                    ParseScriptAttribute(attr, manifest);
                }
            }

            // Check for #Requires -RunAsAdministrator
            var requiresAdmin = scriptContent.Contains("#Requires -RunAsAdministrator", StringComparison.OrdinalIgnoreCase);
            if (requiresAdmin)
            {
                manifest.RequiresAdmin = true;
            }

            // Parse parameters from param block
            if (paramBlock?.Parameters is not null)
            {
                foreach (var param in paramBlock.Parameters)
                {
                    var scriptParam = ParseParameter(param, helpContent);
                    manifest.Parameters.Add(scriptParam);
                }
            }

            return manifest;
        }
        catch
        {
            return null;
        }
    }

    public static string? ExpandPathTokens(string? path, string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        var scriptDir = Path.GetDirectoryName(scriptPath) ?? "";
        return path.Replace("{ScriptDir}", scriptDir)
                   .Replace("{Home}", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                   .Replace("{Temp}", Path.GetTempPath());
    }

    private static bool ParseScriptAttribute(AttributeAst attr, ScriptManifest manifest)
    {
        var typeName = attr.TypeName.Name;

        switch (typeName)
        {
            case "ScriptHost" when TryGetStringArgument(attr, 0, out var host):
                manifest.Host = host;
                return true;

            case "ScriptCwd" when TryGetStringArgument(attr, 0, out var cwd):
                manifest.Cwd = cwd;
                return true;

            case "RequiresElevation":
                manifest.RequiresAdmin = true;
                return true;

            case "ScriptTimeout" when TryGetIntArgument(attr, 0, out var timeout):
                manifest.TimeoutMs = timeout;
                return true;

            case "ScriptMutex" when TryGetStringArgument(attr, 0, out var mutex):
                manifest.Mutex = mutex;
                return true;

            case "ScriptOutput" when TryGetStringArgument(attr, 0, out var output):
                manifest.Output = output;
                return true;

            case "ScriptOutputAction" when TryGetStringArgument(attr, 0, out var action):
                manifest.OutputAction = action;
                return true;

            case "ScriptIcon" when TryGetStringArgument(attr, 0, out var icon):
                manifest.IconGlyph = icon;
                return true;

            case "ScriptEnv" when TryGetStringArgument(attr, 0, out var envName) && TryGetStringArgument(attr, 1, out var envValue):
                manifest.Env[envName] = envValue;
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetStringArgument(AttributeAst attr, int index, out string? value)
    {
        value = null;
        if (attr.PositionalArguments is null || attr.PositionalArguments.Count <= index)
            return false;

        value = attr.PositionalArguments[index].SafeGetValue()?.ToString();
        return value is not null;
    }

    private static bool TryGetIntArgument(AttributeAst attr, int index, out int value)
    {
        value = 0;
        if (attr.PositionalArguments is null || attr.PositionalArguments.Count <= index)
            return false;

        var strValue = attr.PositionalArguments[index].SafeGetValue()?.ToString();
        return int.TryParse(strValue, out value);
    }

    private static bool TryGetStringArrayArgument(AttributeAst attr, int index, out string[] value)
    {
        value = [];
        if (attr.PositionalArguments is null || attr.PositionalArguments.Count <= index)
            return false;

        var arg = attr.PositionalArguments[index];
        if (arg is ArrayLiteralAst arrayAst)
        {
            value = arrayAst.Elements
                .Select(e => e.SafeGetValue()?.ToString())
                .Where(s => s is not null)
                .ToArray()!;
            return value.Length > 0;
        }

        var singleValue = arg.SafeGetValue()?.ToString();
        if (singleValue is not null)
        {
            value = [singleValue];
            return true;
        }

        return false;
    }

    private static ScriptParameter ParseParameter(ParameterAst paramAst, CommentHelpInfo? helpContent)
    {
        var paramName = paramAst.Name.VariablePath.UserPath;
        
        // Find [Parameter(...)] attribute
        var paramAttribute = paramAst.Attributes
            .OfType<AttributeAst>()
            .FirstOrDefault(a => a.TypeName.Name == "Parameter");

        bool isMandatory = false;
        string? helpMessage = null;

        if (paramAttribute?.NamedArguments is not null)
        {
            foreach (var namedArg in paramAttribute.NamedArguments)
            {
                if (namedArg.ArgumentName.Equals("Mandatory", StringComparison.OrdinalIgnoreCase))
                {
                    isMandatory = namedArg.Argument.SafeGetValue()?.ToString()?.Equals("True", StringComparison.OrdinalIgnoreCase) ?? false;
                }
                else if (namedArg.ArgumentName.Equals("HelpMessage", StringComparison.OrdinalIgnoreCase))
                {
                    helpMessage = namedArg.Argument.SafeGetValue()?.ToString();
                }
            }
        }

        // Get description from comment-based help
        string? paramHelp = null;
        if (helpContent?.Parameters is not null && helpContent.Parameters.ContainsKey(paramName))
        {
            paramHelp = helpContent.Parameters[paramName];
        }

        // Determine type from AST
        var psType = DeterminePowerShellType(paramAst);
        var uiType = MapPowerShellTypeToUiType(psType, paramAst);

        // Get default value
        object? defaultValue = null;
        if (paramAst.DefaultValue is not null)
        {
            defaultValue = TryGetDefaultValue(paramAst.DefaultValue);
        }

        // Extract validation attributes for enums, ranges, etc.
        var (options, min, max) = ExtractValidationInfo(paramAst);

        var scriptParam = new ScriptParameter
        {
            Name = paramName,
            Type = uiType,
            Label = helpMessage ?? paramHelp ?? paramName,
            Default = defaultValue,
            Required = isMandatory ? true : null,
            Options = options,
            Min = min,
            Max = max
        };

        return scriptParam;
    }

    private static string DeterminePowerShellType(ParameterAst paramAst)
    {
        // Check for explicit type constraint
        var typeConstraint = paramAst.Attributes
            .OfType<TypeConstraintAst>()
            .FirstOrDefault();

        if (typeConstraint is not null)
        {
            var typeName = typeConstraint.TypeName.Name;
            return typeName;
        }

        // Check if it's a switch
        if (paramAst.Attributes.Any(a => a is AttributeAst attr && attr.TypeName.Name == "switch"))
            return "switch";

        return "string"; // Default to string
    }

    private static string MapPowerShellTypeToUiType(string psType, ParameterAst paramAst)
    {
        // Check for ValidateSet first (indicates enum)
        var validateSet = paramAst.Attributes
            .OfType<AttributeAst>()
            .FirstOrDefault(a => a.TypeName.Name == "ValidateSet");
        
        if (validateSet is not null)
            return "enum";

        return psType.ToLowerInvariant() switch
        {
            "switch" or "bool" or "boolean" => "bool",
            "int" or "int32" or "int64" or "long" => "int",
            "double" or "float" or "decimal" => "number",
            "string" => "string",
            _ => "string"
        };
    }

    private static object? TryGetDefaultValue(ExpressionAst expr)
    {
        try
        {
            return expr.SafeGetValue();
        }
        catch
        {
            // If we can't get the value, try to extract the text representation
            return expr.Extent.Text.Trim('"', '\'');
        }
    }

    private static (List<string>? options, double? min, double? max) ExtractValidationInfo(ParameterAst paramAst)
    {
        List<string>? options = null;
        double? min = null;
        double? max = null;

        foreach (var attr in paramAst.Attributes.OfType<AttributeAst>())
        {
            switch (attr.TypeName.Name)
            {
                case "ValidateSet":
                    options = ExtractValidateSetOptions(attr);
                    break;

                case "ValidateRange":
                    (min, max) = ExtractValidateRange(attr);
                    break;
            }
        }

        return (options, min, max);
    }

    private static List<string>? ExtractValidateSetOptions(AttributeAst attr)
    {
        if (attr.PositionalArguments is null || attr.PositionalArguments.Count == 0)
            return null;

        var options = new List<string>();
        foreach (var arg in attr.PositionalArguments)
        {
            var value = arg.SafeGetValue()?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                options.Add(value);
        }

        return options.Count > 0 ? options : null;
    }

    private static (double? min, double? max) ExtractValidateRange(AttributeAst attr)
    {
        if (attr.PositionalArguments is null || attr.PositionalArguments.Count < 2)
            return (null, null);

        double? min = null;
        double? max = null;

        if (double.TryParse(attr.PositionalArguments[0].SafeGetValue()?.ToString(), out var minVal))
            min = minVal;

        if (double.TryParse(attr.PositionalArguments[1].SafeGetValue()?.ToString(), out var maxVal))
            max = maxVal;

        return (min, max);
    }
}

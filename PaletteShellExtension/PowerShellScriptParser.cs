using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PaletteShellExtension.Classes;

namespace PaletteShellExtension;

/// <summary>
/// Parses PaletteShell metadata out of a <c>.ps1</c> file: comment-based help
/// (<c>.SYNOPSIS</c>/<c>.DESCRIPTION</c>/<c>.PARAMETER</c>), the script-level
/// <c>[Script*]</c> attributes, and the <c>param(...)</c> block.
/// </summary>
/// <remarks>
/// This is a deliberately lightweight text parser. It intentionally does NOT host the
/// PowerShell engine (<c>System.Management.Automation</c>) — that SDK pulls in ~150
/// dependency assemblies and roughly tripled the packaged size. Scripts are still
/// executed by shelling out to <c>pwsh.exe</c>/<c>powershell.exe</c>; this only needs to
/// read the metadata, which follows a regular, well-known shape.
/// </remarks>
internal static partial class PowerShellScriptParser
{
    public static ScriptManifest? TryParseManifest(string ps1Path)
    {
        if (!File.Exists(ps1Path))
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(ps1Path, Encoding.UTF8);

            var help = ParseCommentHelp(content);

            var manifest = new ScriptManifest
            {
                Title = string.IsNullOrWhiteSpace(help.Synopsis)
                    ? Path.GetFileNameWithoutExtension(ps1Path)
                    : help.Synopsis!,
                Description = help.Description,
                Parameters = []
            };

            // Strip comments so structural parsing isn't confused by help text or
            // per-parameter line comments, then locate the param(...) block.
            var cleaned = RemoveComments(content);
            var paramKeyword = ParamKeywordRegex().Match(cleaned);

            string attributeZone;
            string? paramBlock = null;
            if (paramKeyword.Success)
            {
                var openParen = cleaned.IndexOf('(', paramKeyword.Index);
                if (openParen >= 0)
                {
                    paramBlock = ExtractBalanced(cleaned, openParen, '(', ')');
                }
                attributeZone = cleaned[..paramKeyword.Index];
            }
            else
            {
                attributeZone = cleaned;
            }

            // Script-level [Script*] attributes live before the param keyword.
            ParseScriptAttributes(attributeZone, manifest);

            // #Requires -RunAsAdministrator (checked against the raw text; the line is a comment)
            if (content.Contains("#Requires -RunAsAdministrator", StringComparison.OrdinalIgnoreCase))
            {
                manifest.RequiresAdmin = true;
            }

            if (paramBlock is not null)
            {
                foreach (var chunk in SplitTopLevel(paramBlock, ','))
                {
                    if (string.IsNullOrWhiteSpace(chunk))
                    {
                        continue;
                    }

                    var parameter = ParseParameter(chunk, help);
                    if (parameter is not null)
                    {
                        manifest.Parameters.Add(parameter);
                    }
                }
            }

            return manifest;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? ExpandPathTokens(string? path, string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var scriptDir = Path.GetDirectoryName(scriptPath) ?? "";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var temp = Path.GetTempPath();

        return path.Replace("{ScriptDir}", scriptDir)
                   .Replace("{Home}", home)
                   .Replace("{Temp}", temp);
    }

    // ----- Comment-based help -----------------------------------------------------------

    private sealed class HelpInfo
    {
        public string? Synopsis { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, string> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static HelpInfo ParseCommentHelp(string content)
    {
        var info = new HelpInfo();

        var block = CommentBlockRegex().Match(content);
        if (!block.Success)
        {
            return info;
        }

        var lines = block.Groups[1].Value.Replace("\r\n", "\n").Split('\n');

        string? section = null; // "SYNOPSIS" | "DESCRIPTION" | "PARAMETER:<name>" | null
        var buffer = new StringBuilder();

        void Flush()
        {
            if (section is not null)
            {
                var text = buffer.ToString().Trim();
                if (text.Length > 0)
                {
                    if (section == "SYNOPSIS")
                    {
                        info.Synopsis = text;
                    }
                    else if (section == "DESCRIPTION")
                    {
                        info.Description = text;
                    }
                    else if (section.StartsWith("PARAMETER:", StringComparison.Ordinal))
                    {
                        info.Parameters[section["PARAMETER:".Length..]] = text;
                    }
                }
            }

            buffer.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var key = HelpKeyRegex().Match(line);
            if (key.Success)
            {
                Flush();
                var keyword = key.Groups["k"].Value.ToUpperInvariant();
                var arg = key.Groups["a"].Value.Trim();
                section = keyword switch
                {
                    "SYNOPSIS" => "SYNOPSIS",
                    "DESCRIPTION" => "DESCRIPTION",
                    "PARAMETER" when arg.Length > 0 => "PARAMETER:" + arg,
                    _ => null
                };
                continue;
            }

            if (section is not null && line.Length > 0)
            {
                if (buffer.Length > 0)
                {
                    buffer.Append(' ');
                }
                buffer.Append(line);
            }
        }

        Flush();
        return info;
    }

    // ----- Script-level attributes ------------------------------------------------------

    private static void ParseScriptAttributes(string zone, ScriptManifest manifest)
    {
        foreach (var group in FindBracketGroups(zone))
        {
            var (name, args) = SplitNameArgs(group);
            var values = args is null
                ? new List<string>()
                : SplitTopLevel(args, ',').Select(StripQuotes).ToList();

            switch (name)
            {
                case "ScriptHost" when values.Count >= 1:
                    manifest.Host = values[0];
                    break;
                case "ScriptCwd" when values.Count >= 1:
                    manifest.Cwd = values[0];
                    break;
                case "RequiresElevation":
                    manifest.RequiresAdmin = true;
                    break;
                case "ScriptTimeout" when values.Count >= 1 && int.TryParse(values[0], out var timeout):
                    manifest.TimeoutMs = timeout;
                    break;
                case "ScriptOutput" when values.Count >= 1:
                    manifest.Output = values[0];
                    break;
                case "ScriptGroup" when values.Count >= 1:
                    manifest.Group = values[0];
                    break;
                case "ScriptIcon" when values.Count >= 1:
                    manifest.IconGlyph = values[0];
                    break;
                case "ScriptEnv" when values.Count >= 2:
                    manifest.Env[values[0]] = values[1];
                    break;
            }
        }
    }

    // ----- Parameters -------------------------------------------------------------------

    private static ScriptParameter? ParseParameter(string chunk, HelpInfo help)
    {
        // Variable name and default value live after the last attribute bracket.
        var lastBracket = LastTopLevelCloseBracket(chunk);
        var tail = lastBracket >= 0 ? chunk[(lastBracket + 1)..] : chunk;

        var nameMatch = VariableRegex().Match(tail);
        if (!nameMatch.Success)
        {
            return null;
        }
        var name = nameMatch.Groups["n"].Value;

        var equals = tail.IndexOf('=', nameMatch.Index + nameMatch.Length);
        var defaultExpr = equals >= 0 ? tail[(equals + 1)..].Trim() : null;

        bool mandatory = false;
        bool allowExpression = false;
        string? helpMessage = null;
        List<string>? options = null;
        double? min = null;
        double? max = null;
        string? typeName = null;

        foreach (var group in FindBracketGroups(chunk))
        {
            var (attrName, args) = SplitNameArgs(group);
            switch (attrName)
            {
                case "Parameter" when args is not null:
                    foreach (var arg in SplitTopLevel(args, ','))
                    {
                        var a = arg.Trim();
                        if (a.StartsWith("Mandatory", StringComparison.OrdinalIgnoreCase))
                        {
                            var eq = a.IndexOf('=');
                            mandatory = eq < 0
                                || a[(eq + 1)..].Trim().TrimStart('$').Equals("true", StringComparison.OrdinalIgnoreCase);
                        }
                        else if (a.StartsWith("HelpMessage", StringComparison.OrdinalIgnoreCase))
                        {
                            var eq = a.IndexOf('=');
                            if (eq >= 0)
                            {
                                helpMessage = StripQuotes(a[(eq + 1)..]);
                            }
                        }
                    }
                    break;

                case "ValidateSet" when args is not null:
                    var opts = SplitTopLevel(args, ',')
                        .Select(StripQuotes)
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .ToList();
                    if (opts.Count > 0)
                    {
                        options = opts;
                    }
                    break;

                case "ValidateRange" when args is not null:
                    var bounds = SplitTopLevel(args, ',').Select(b => b.Trim()).ToList();
                    if (bounds.Count >= 2)
                    {
                        if (double.TryParse(bounds[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var lo))
                        {
                            min = lo;
                        }
                        if (double.TryParse(bounds[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var hi))
                        {
                            max = hi;
                        }
                    }
                    break;

                case "AllowExpression":
                    allowExpression = true;
                    break;

                default:
                    // A bracket with no arguments that isn't a known attribute is the type
                    // constraint (e.g. [string], [int], [switch]); the one nearest the
                    // variable wins.
                    if (args is null && !KnownAttributes.Contains(attrName))
                    {
                        typeName = attrName;
                    }
                    break;
            }
        }

        var uiType = MapType(typeName ?? "string", options is not null);

        object? defaultValue = string.IsNullOrEmpty(defaultExpr)
            ? null
            : InterpretDefault(defaultExpr!, uiType);

        var paramHelp = help.Parameters.TryGetValue(name, out var ph) ? ph : null;

        return new ScriptParameter
        {
            Name = name,
            Type = uiType,
            Label = helpMessage ?? paramHelp ?? name,
            Default = defaultValue,
            Required = mandatory ? true : null,
            Options = options,
            Min = min,
            Max = max,
            AllowExpression = allowExpression
        };
    }

    private static string MapType(string psType, bool hasValidateSet)
    {
        if (hasValidateSet)
        {
            return "enum";
        }

        return psType.ToLowerInvariant() switch
        {
            "switch" or "bool" or "boolean" => "bool",
            "int" or "int32" or "int64" or "long" => "int",
            "double" or "float" or "single" or "decimal" => "number",
            _ => "string"
        };
    }

    private static object? InterpretDefault(string expr, string uiType)
    {
        expr = expr.Trim();

        if (expr.Equals("$null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var isTrue = expr.Equals("$true", StringComparison.OrdinalIgnoreCase) || expr.Equals("true", StringComparison.OrdinalIgnoreCase);
        var isFalse = expr.Equals("$false", StringComparison.OrdinalIgnoreCase) || expr.Equals("false", StringComparison.OrdinalIgnoreCase);

        switch (uiType)
        {
            case "bool":
                return isTrue;
            case "int":
                var i = StripQuotes(expr);
                return int.TryParse(i, NumberStyles.Any, CultureInfo.InvariantCulture, out var iv) ? iv : i;
            case "number":
                var n = StripQuotes(expr);
                return double.TryParse(n, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv) ? dv : n;
            default:
                if (isTrue)
                {
                    return true;
                }
                if (isFalse)
                {
                    return false;
                }
                return StripQuotes(expr);
        }
    }

    // ----- Low-level text helpers -------------------------------------------------------

    private static readonly HashSet<string> KnownAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Parameter", "ValidateSet", "ValidateRange", "ValidatePattern", "ValidateLength",
        "ValidateCount", "ValidateNotNull", "ValidateNotNullOrEmpty", "ValidateScript",
        "ValidateDrive", "ValidateUserDrive", "AllowNull", "AllowEmptyString", "AllowExpression",
        "AllowEmptyCollection", "CmdletBinding", "OutputType", "Alias", "SupportsWildcards",
        "PSDefaultValue", "ArgumentCompleter",
        "ScriptHost", "ScriptCwd", "RequiresElevation", "ScriptTimeout",
        "ScriptOutput", "ScriptIcon", "ScriptGroup", "ScriptEnv"
    };

    /// <summary>Removes <c>&lt;# ... #&gt;</c> blocks and whole-line <c>#</c> comments.</summary>
    private static string RemoveComments(string content)
    {
        var withoutBlocks = CommentBlockRegex().Replace(content, "");

        var builder = new StringBuilder();
        foreach (var line in withoutBlocks.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith('#'))
            {
                continue;
            }
            builder.Append(line).Append('\n');
        }
        return builder.ToString();
    }

    /// <summary>Returns the inner text of each top-level <c>[...]</c> group, quote-aware.</summary>
    private static List<string> FindBracketGroups(string s)
    {
        var groups = new List<string>();
        int depth = 0;
        int start = -1;
        char quote = '\0';

        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            switch (c)
            {
                case '\'' or '"':
                    quote = c;
                    break;
                case '[':
                    if (depth == 0)
                    {
                        start = i + 1;
                    }
                    depth++;
                    break;
                case ']':
                    if (depth > 0)
                    {
                        depth--;
                        if (depth == 0 && start >= 0)
                        {
                            groups.Add(s[start..i]);
                            start = -1;
                        }
                    }
                    break;
            }
        }

        return groups;
    }

    private static int LastTopLevelCloseBracket(string s)
    {
        int depth = 0;
        int last = -1;
        char quote = '\0';

        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '[')
            {
                depth++;
            }
            else if (c == ']' && depth > 0)
            {
                depth--;
                if (depth == 0)
                {
                    last = i;
                }
            }
        }

        return last;
    }

    /// <summary>Splits on <paramref name="separator"/> at the top level, ignoring separators
    /// nested inside <c>() [] {}</c> or quotes.</summary>
    private static IEnumerable<string> SplitTopLevel(string s, char separator)
    {
        int paren = 0, bracket = 0, brace = 0;
        char quote = '\0';
        int start = 0;

        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '(')
            {
                paren++;
            }
            else if (c == ')' && paren > 0)
            {
                paren--;
            }
            else if (c == '[')
            {
                bracket++;
            }
            else if (c == ']' && bracket > 0)
            {
                bracket--;
            }
            else if (c == '{')
            {
                brace++;
            }
            else if (c == '}' && brace > 0)
            {
                brace--;
            }
            else if (c == separator && paren == 0 && bracket == 0 && brace == 0)
            {
                yield return s[start..i];
                start = i + 1;
            }
        }

        yield return s[start..];
    }

    /// <summary>Returns the content between the delimiter at <paramref name="openIndex"/> and its
    /// matching close, or null if unbalanced. Quote-aware.</summary>
    private static string? ExtractBalanced(string s, int openIndex, char open, char close)
    {
        int depth = 0;
        char quote = '\0';

        for (int i = openIndex; i < s.Length; i++)
        {
            var c = s[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return s[(openIndex + 1)..i];
                }
            }
        }

        return null;
    }

    /// <summary>Splits an attribute body like <c>Parameter(Mandatory=$true)</c> into name and
    /// argument text; returns null args when there are no parentheses.</summary>
    private static (string Name, string? Args) SplitNameArgs(string group)
    {
        var open = group.IndexOf('(');
        if (open < 0)
        {
            return (group.Trim(), null);
        }

        var name = group[..open].Trim();
        var args = ExtractBalanced(group, open, '(', ')');
        return (name, args);
    }

    /// <summary>Trims one matching pair of surrounding quotes and unescapes doubled quotes.</summary>
    private static string StripQuotes(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && (value[0] == '\'' || value[0] == '"') && value[^1] == value[0])
        {
            var q = value[0];
            value = value[1..^1];
            value = q == '\'' ? value.Replace("''", "'") : value.Replace("\"\"", "\"");
        }
        return value;
    }

    [GeneratedRegex(@"<#(.*?)#>", RegexOptions.Singleline)]
    private static partial Regex CommentBlockRegex();

    [GeneratedRegex(@"^\.(?<k>[A-Za-z]+)(?:\s+(?<a>\S+))?\s*$")]
    private static partial Regex HelpKeyRegex();

    [GeneratedRegex(@"\bparam\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex ParamKeywordRegex();

    [GeneratedRegex(@"\$\{?(?<n>[A-Za-z_]\w*)\}?")]
    private static partial Regex VariableRegex();
}

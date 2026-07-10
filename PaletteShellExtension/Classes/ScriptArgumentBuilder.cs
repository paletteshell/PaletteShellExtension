using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Builds the PowerShell argument line for a run, in one place, so every surface quotes values the
/// same way. Values are single-quoted literals (so <c>$</c>, <c>;</c>, backticks and quotes reach
/// the script intact rather than being evaluated) unless the parameter is a bool or is marked
/// <c>[AllowExpression]</c>.
/// </summary>
internal static class ScriptArgumentBuilder
{
    /// <summary>Builds <c>-Name value</c> pairs from an Adaptive Card's submitted input object,
    /// skipping empty optional parameters.</summary>
    public static string BuildFromForm(IReadOnlyList<ScriptParameter> parameters, JsonObject values)
    {
        var args = new List<string>();
        foreach (var param in parameters)
        {
            var value = values[param.Name]?.ToString();

            // A [switch] is supplied by presence: emit bare -Name when on, omit entirely when
            // off. Emitting -Name $false (as [bool] does) would mis-bind, and a false toggle
            // would wrongly register in $PSBoundParameters.
            if (param.Type == "switch")
            {
                if (value != null && value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    args.Add($"-{param.Name}");
                continue;
            }

            // Skip empty optional parameters so the script's own default applies.
            if (string.IsNullOrWhiteSpace(value) && param.Required != true)
                continue;

            args.Add($"-{param.Name}");
            args.Add(FormatArgValue(param, value ?? ""));
        }

        return string.Join(" ", args);
    }

    /// <summary>Builds the single <c>-Query</c> argument that passes a live List provider's search
    /// text. Empty text yields an empty line so the script's own default applies.</summary>
    public static string BuildQueryArg(string queryParam, string? query)
        => string.IsNullOrEmpty(query)
            ? ""
            : $"-{queryParam} {PowerShellQuoting.SingleQuote(query)}";

    /// <summary>
    /// Formats a single value as a PowerShell command-line argument. Booleans become
    /// <c>$true</c>/<c>$false</c>; parameters marked <c>[AllowExpression]</c> are injected verbatim
    /// so PowerShell evaluates them; everything else is a single-quoted literal.
    /// </summary>
    public static string FormatArgValue(ScriptParameter param, string value)
    {
        if (param.Type == "bool")
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "$true" : "$false";

        if (param.AllowExpression)
            return value;

        return PowerShellQuoting.SingleQuote(value);
    }
}

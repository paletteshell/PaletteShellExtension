using System;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Single source of truth for how elevation and output capture interact. Elevation launches
/// the process with <c>runas</c> + <c>UseShellExecute=true</c>, which cannot redirect stdout —
/// so the only output mode compatible with elevation is <c>None</c>. Every execution route
/// consults these helpers so the rule can't drift between routes.
/// </summary>
internal static class ScriptElevation
{
    /// <summary>True when the script asked to run elevated (via <c>[RequiresElevation()]</c>
    /// or <c>#Requires -RunAsAdministrator</c>).</summary>
    public static bool RequiresElevation(ScriptManifest manifest)
        => manifest.RequiresAdmin == true;

    /// <summary>True when the declared output mode needs captured stdout. <c>None</c> is the
    /// only non-capturing mode; every other mode (Toast/Clipboard/Markdown/Result/List/Open/
    /// File, or any unrecognized value that falls through to Toast) surfaces output.</summary>
    public static bool CapturesOutput(ScriptManifest manifest)
        => !string.IsNullOrWhiteSpace(manifest.Output)
           && !string.Equals(manifest.Output, "None", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the script wants elevation but also an output mode that needs
    /// capture — an impossible combination that must be blocked rather than run unelevated.</summary>
    public static bool IsElevatedOutputIncompatible(ScriptManifest manifest)
        => RequiresElevation(manifest) && CapturesOutput(manifest);

    /// <summary>Subtitle shown on the blocked row and in its explanatory toast.</summary>
    public static string IncompatibleReason()
        => "⚠ Elevated scripts can't return output — use [ScriptOutput('None')] or remove elevation";
}

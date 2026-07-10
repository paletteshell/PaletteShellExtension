namespace PaletteShellExtension.Classes;

/// <summary>The outcome kind of a pre-run compatibility check.</summary>
internal enum ScriptCompatibilityKind
{
    /// <summary>The script can run on this route.</summary>
    Ok,

    /// <summary>The running app version is outside the script's declared min/max range.</summary>
    RequiresUpdate,

    /// <summary>The script asks to run elevated but also declares a capturing output mode — an
    /// impossible combination (elevation can't redirect stdout).</summary>
    ElevationIncompatible,

    /// <summary>The script declares a <c>[ScriptHost(...)]</c> value that isn't a recognized
    /// interpreter (not auto/pwsh/powershell). Caught at discovery so it never runs under the
    /// wrong shell via a silent fallback.</summary>
    UnknownHost,
}

/// <summary>
/// The single place a script's manifest is checked for runnability before a route is chosen —
/// app-version range and the elevation/output-capture conflict. Collapses the two gates that used
/// to sit inline in the routing loop into one call so the checks can't drift.
/// </summary>
internal readonly record struct ScriptCompatibility(
    ScriptCompatibilityKind Kind,
    string? RequiredVersion = null,
    bool TooNew = false,
    string? BadHost = null)
{
    private static readonly ScriptCompatibility Compatible = new(ScriptCompatibilityKind.Ok);

    public static ScriptCompatibility Validate(ScriptManifest? manifest)
    {
        if (manifest is null)
            return Compatible;

        if (!AppVersion.IsCompatible(manifest.MinVersion, manifest.MaxVersion, out var required, out var tooNew))
            return new ScriptCompatibility(ScriptCompatibilityKind.RequiresUpdate, required!.ToString(), tooNew);

        // A declared host that parses to Unknown is a manifest error: block it here rather than
        // let the runner pick an interpreter the script never asked for. Null means "no override"
        // (the configured default applies), which is always a valid token.
        if (manifest.Host is not null && ScriptRunner.ParseHost(manifest.Host) == ScriptRunner.ShellHost.Unknown)
            return new ScriptCompatibility(ScriptCompatibilityKind.UnknownHost, BadHost: manifest.Host);

        if (ScriptElevation.IsElevatedOutputIncompatible(manifest))
            return new ScriptCompatibility(ScriptCompatibilityKind.ElevationIncompatible);

        return Compatible;
    }
}

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

    /// <summary>The script's effective host is PowerShell 7 (<c>pwsh</c>), but pwsh.exe isn't
    /// installed on this machine. Blocked at discovery so it shows a "install pwsh" warning
    /// instead of a runnable row that fails on click. Note <c>auto</c> is never blocked — it
    /// falls back to Windows PowerShell.</summary>
    PwshMissing,
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

    /// <param name="pwshAvailable">Whether pwsh.exe was found on this machine. The caller probes
    /// once per refresh (<see cref="ScriptRunner.IsPwshInstalled"/>) and passes the result in so the
    /// per-script validation stays a pure check with no filesystem I/O.</param>
    /// <param name="defaultHost">The configured default host applied when a script declares none —
    /// needed to resolve the effective host for the pwsh-missing gate.</param>
    public static ScriptCompatibility Validate(ScriptManifest? manifest, bool pwshAvailable, string? defaultHost)
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

        // A script whose effective host resolves to pwsh (declared, or via the default) can't run
        // if pwsh 7 isn't installed — ResolveShell would throw. Block it up front with an
        // actionable warning. `auto` is deliberately not caught here: it falls back to 5.1.
        var effectiveHost = manifest.Host ?? defaultHost;
        if (!pwshAvailable && ScriptRunner.ParseHost(effectiveHost) == ScriptRunner.ShellHost.Pwsh)
            return new ScriptCompatibility(ScriptCompatibilityKind.PwshMissing);

        if (ScriptElevation.IsElevatedOutputIncompatible(manifest))
            return new ScriptCompatibility(ScriptCompatibilityKind.ElevationIncompatible);

        return Compatible;
    }
}

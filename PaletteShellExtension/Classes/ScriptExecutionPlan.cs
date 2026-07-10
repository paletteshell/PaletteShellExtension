using System;
using System.Collections.Generic;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Immutable snapshot of every decision needed to run a script — host, working directory,
/// environment, timeout, elevation, output mode. Built once by
/// <see cref="ScriptExecutionService.CreatePlan"/> from a manifest so no execution route
/// re-derives these (which is what let elevation drift between routes). Callers supply only the
/// per-invocation argument line.
/// </summary>
internal sealed class ScriptExecutionPlan
{
    public required string ScriptPath { get; init; }

    /// <summary>Interpreter host token (<c>"auto"</c>/<c>"pwsh"</c>/<c>"powershell"</c>), resolved
    /// from the manifest with the configured default folded in. <see cref="ScriptRunner"/> turns
    /// this into the actual executable.</summary>
    public required string Host { get; init; }

    public string? Cwd { get; init; }

    /// <summary>Environment overrides with path tokens ({ScriptDir}/{Home}/{Temp}) already expanded.</summary>
    public required Dictionary<string, string> Env { get; init; }

    public IReadOnlyList<string> RequiredModules { get; init; } = [];

    public string OutputMode { get; init; } = "None";

    public string? FileExtension { get; init; }

    /// <summary>Single canonical elevation decision (from <see cref="ScriptElevation.RequiresElevation"/>),
    /// so no route hardcodes it.</summary>
    public bool RequiresAdmin { get; init; }

    /// <summary>The script's declared <c>[ScriptTimeout(...)]</c>, or null when it declared none.
    /// Null lets the no-output fire-and-forget shortcut apply; <see cref="EffectiveTimeoutMs"/>
    /// folds in the configured default for the waiting paths.</summary>
    public int? DeclaredTimeoutMs { get; init; }

    /// <summary>The declared timeout, or the configured default when the script declared none.
    /// Every waiting run is bounded by this.</summary>
    public int EffectiveTimeoutMs { get; init; }

    /// <summary>Elevated runs use <c>runas</c> + <c>UseShellExecute</c>, which can't redirect
    /// stdout, so only an unelevated run captures output.</summary>
    public bool CaptureOutput => !RequiresAdmin;

    /// <summary>True when the declared output mode needs the script's stdout (anything but None).</summary>
    public bool SurfacesOutput
        => !string.Equals(OutputMode, "None", StringComparison.OrdinalIgnoreCase);
}

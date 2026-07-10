using System.Collections.Generic;
using System.Linq;

namespace PaletteShellExtension.Classes;

/// <summary>How serious a <see cref="ScriptParseDiagnostic"/> is.</summary>
public enum ScriptParseSeverity
{
    /// <summary>Something was ignored, but the manifest is still safe to run
    /// (e.g. an invalid <c>[ScriptIcon(...)]</c> glyph fell back to the default).</summary>
    Warning,

    /// <summary>The metadata could not be understood well enough to run the script safely.
    /// Any error makes the parse <em>fail closed</em>: <see cref="ScriptParseResult.Manifest"/>
    /// is null and the script must be surfaced as needing repair rather than executed.</summary>
    Error,
}

/// <summary>A single problem found while parsing a script's metadata.</summary>
public sealed record ScriptParseDiagnostic(ScriptParseSeverity Severity, string Message);

/// <summary>
/// Outcome of parsing a script's PaletteShell metadata. Replaces the old nullable-manifest
/// return so callers can tell "no metadata, but fine to run" apart from "metadata is malformed —
/// do not run." A non-empty set of <see cref="ScriptParseSeverity.Error"/> diagnostics means the
/// parser <em>failed closed</em>: <see cref="Manifest"/> is null and the script should be shown as
/// a disabled repair row instead of executed.
/// </summary>
public sealed record ScriptParseResult(
    ScriptManifest? Manifest,
    IReadOnlyList<ScriptParseDiagnostic> Diagnostics,
    bool WasTruncated)
{
    /// <summary>True when the metadata was malformed enough that the script must not run.</summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == ScriptParseSeverity.Error);

    /// <summary>The first blocking message, for a repair row's subtitle; null when none.</summary>
    public string? FirstError =>
        Diagnostics.FirstOrDefault(d => d.Severity == ScriptParseSeverity.Error)?.Message;
}

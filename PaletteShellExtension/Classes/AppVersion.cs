using System;
using System.Reflection;
using Windows.ApplicationModel;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Resolves the running PaletteShell version, and checks a script's
/// <c>[RequiresPaletteShellMinimum(...)]</c> / <c>[RequiresPaletteShellMaximum(...)]</c> against
/// it so scripts written for a newer app don't run (or fail confusingly) on an older one, and so
/// scripts that depend on since-removed behavior can rule out running on a too-new one.
/// </summary>
internal static class AppVersion
{
    /// <summary>The installed app's version. Read from the MSIX package identity (matches
    /// <c>Package.appxmanifest</c>) with a fallback to the assembly version for contexts where
    /// there's no package identity, e.g. unit tests.</summary>
    public static Version Current { get; } = ResolveCurrent();

    private static Version ResolveCurrent()
    {
        try
        {
            var v = Package.Current.Id.Version;
            return new Version(v.Major, v.Minor, v.Build, v.Revision);
        }
        catch (Exception)
        {
            return typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        }
    }

    /// <summary>
    /// True when <paramref name="minVersion"/> is unset, unparseable (fails open rather than
    /// hiding a script over a typo'd attribute), or no greater than <see cref="Current"/>.
    /// </summary>
    public static bool IsCompatible(string? minVersion, out Version? required) =>
        IsCompatible(minVersion, null, out required, out _);

    /// <summary>
    /// Same as the single-argument overload, but also checks <paramref name="maxVersion"/>: false
    /// (with <paramref name="tooNew"/> set) when <see cref="Current"/> is strictly greater than
    /// it. An unset or unparseable <paramref name="maxVersion"/> never blocks - same fail-open
    /// stance as <paramref name="minVersion"/>. When both bounds are violated, the minimum wins
    /// (a script can't be both too old and too new for a single install).
    /// </summary>
    public static bool IsCompatible(string? minVersion, string? maxVersion, out Version? required, out bool tooNew)
    {
        tooNew = false;

        if (!string.IsNullOrWhiteSpace(minVersion))
        {
            if (!Version.TryParse(minVersion, out var min))
            {
                Log.Warn($"Ignoring invalid RequiresPaletteShellMinimum '{minVersion}' — expected a dotted version like '1.2.0'");
            }
            else if (Current < min)
            {
                required = min;
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(maxVersion))
        {
            if (!Version.TryParse(maxVersion, out var max))
            {
                Log.Warn($"Ignoring invalid RequiresPaletteShellMaximum '{maxVersion}' — expected a dotted version like '1.2.0'");
            }
            else if (Current > max)
            {
                required = max;
                tooNew = true;
                return false;
            }
        }

        required = null;
        return true;
    }
}

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Stands in for a script's normal command when its <c>[RequiresPaletteShellMinimum(...)]</c> or
/// <c>[RequiresPaletteShellMaximum(...)]</c> rules out the running PaletteShell, so selecting the
/// row explains why instead of running a script that may rely on host features/behavior that
/// don't exist yet (or were since removed).
/// </summary>
internal sealed partial class IncompatibleScriptCommand(string requiredVersion, string installedVersion, bool tooNew = false) : InvokableCommand
{
    public override string Name => "Requires an update";
    public override IconInfo Icon => new(""); // Warning

    public override CommandResult Invoke() =>
        CommandResult.ShowToast(tooNew
            ? $"This script requires PaletteShell v{requiredVersion} or earlier (you have v{installedVersion})"
            : $"This script requires PaletteShell v{requiredVersion} or later (you have v{installedVersion})");
}

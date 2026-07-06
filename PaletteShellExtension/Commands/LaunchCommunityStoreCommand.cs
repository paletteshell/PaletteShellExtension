using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Diagnostics;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Opens the companion "PaletteShell Script Store" app (a separate, Store-distributed WinUI 3
/// app that owns browsing/installing/updating community scripts) via its registered protocol,
/// so this extension doesn't need to know where - or whether - it's installed. Falls back to
/// the community scripts GitHub repo if the protocol has no handler, i.e. the app isn't installed yet - same
/// best-effort, never-throw philosophy as the rest of this codebase (see
/// <see cref="Classes.CommunityScriptCatalog"/>'s error handling).
/// </summary>
internal sealed partial class LaunchCommunityStoreCommand : InvokableCommand
{
    private const string CommunityScriptsRepositoryUrl = "https://github.com/paletteshell/PaletteShellScripts";
    private const string ProtocolUrl = "palette-script-manager:";

    public override string Name => "Open";
    public override IconInfo Icon => new("☁");

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ProtocolUrl) { UseShellExecute = true });
            return CommandResult.Dismiss();
        }
        catch (Exception)
        {
            // No handler registered for the protocol - the companion app isn't installed.
            // Send the user to the community repo instead of failing silently.
            try
            {
                Process.Start(new ProcessStartInfo(CommunityScriptsRepositoryUrl) { UseShellExecute = true });
                return CommandResult.ShowToast("PaletteShell Script Store isn't installed - opening the community scripts repo.");
            }
            catch (Exception ex)
            {
                return CommandResult.ShowToast($"Couldn't open the Script Store or community scripts repo: {ex.Message}");
            }
        }
    }
}

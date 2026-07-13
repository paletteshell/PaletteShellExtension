using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
using System.Diagnostics;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Takes the user to the "PaletteShell Script Manager" Microsoft Store listing so they can
/// install it. Prefers the ms-windows-store: deep link (opens the Store app straight to the
/// listing) and falls back to the apps.microsoft.com URL for machines where the Store app
/// itself is unavailable. Best-effort, never-throw - matches the rest of this codebase.
/// </summary>
internal sealed partial class InstallScriptManagerCommand : InvokableCommand
{
    // Microsoft Store product ID for "PaletteShell Script Manager".
    private const string StoreProductId = "9P4TBJKKNFZ1";
    private const string StoreProtocolUrl = "ms-windows-store://pdp/?ProductId=" + StoreProductId;
    private const string StoreWebUrl = "https://apps.microsoft.com/detail/" + StoreProductId;

    public override string Name => "Install";
    public override IconInfo Icon => new(""); // Download

    public override CommandResult Invoke()
    {
        if (TryOpen(StoreProtocolUrl) || TryOpen(StoreWebUrl))
        {
            return CommandResult.Dismiss();
        }

        return CommandResult.ShowToast("Couldn't open the Microsoft Store listing.");
    }

    private static bool TryOpen(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to open Store listing '{url}': {ex.Message}");
            return false;
        }
    }
}

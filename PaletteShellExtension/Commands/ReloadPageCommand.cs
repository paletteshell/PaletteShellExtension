using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Diagnostics;

namespace PaletteShellExtension.Commands;

internal sealed partial class ReloadPageCommand(PaletteShellExtensionPage page) : InvokableCommand
{
    
    public override string Name => "Reload scripts";
    public override IconInfo Icon => new("\uE72C"); // Refresh
    public override CommandResult Invoke()
    {
        Debug.WriteLine("[RELOAD] ===== ReloadPageCommand.Invoke START =====");

        try
        {
            Debug.WriteLine("[RELOAD] Calling page.RefreshFiles()...");
            page.RefreshFiles();
            Debug.WriteLine("[RELOAD] page.RefreshFiles() completed");

            Debug.WriteLine("[RELOAD] Creating CommandResult.KeepOpen()...");
            var result = CommandResult.KeepOpen();
            Debug.WriteLine("[RELOAD] CommandResult.KeepOpen() created successfully");

            Debug.WriteLine("[RELOAD] ===== ReloadPageCommand.Invoke END (Success) =====");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RELOAD] EXCEPTION: {ex.GetType().FullName}");
            Debug.WriteLine($"[RELOAD] Message: {ex.Message}");
            Debug.WriteLine($"[RELOAD] Stack: {ex.StackTrace}");
            Debug.WriteLine("[RELOAD] ===== ReloadPageCommand.Invoke END (Exception) =====");
            throw;
        }
    }
}

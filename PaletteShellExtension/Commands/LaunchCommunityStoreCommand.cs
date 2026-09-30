using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;
using PaletteShellExtension.Classes;
using System;
using System.Diagnostics;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Opens the companion "PaletteShell Script Manager" app (a separate, Store-distributed WinUI 3
/// app that owns browsing/installing/updating community scripts) via its registered protocol,
/// so this extension doesn't need to know where - or whether - it's installed. This command is
/// only used when the app is known to be installed (see <see cref="IsScriptManagerInstalled"/>);
/// when it isn't, the page routes to <see cref="Pages.CommunityScriptsPage"/> so the user can
/// choose between installing it and browsing the repo, rather than presuming they want the Store.
/// Same best-effort, never-throw philosophy as the rest of this codebase.
/// </summary>
internal sealed partial class LaunchCommunityStoreCommand : InvokableCommand
{
    internal const string ProtocolScheme = "palette-script-manager";
    private const string ProtocolUrl = ProtocolScheme + ":";

    public override string Name => "Open";
    public override IconInfo Icon => new("☁");

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ProtocolUrl) { UseShellExecute = true });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to launch Script Manager protocol: {ex.Message}");
            return CommandResult.ShowToast($"Couldn't open the Script Manager: {ex.Message}");
        }
    }

    /// <summary>
    /// True when the Script Manager's URL protocol has a handler, i.e. the app is installed.
    /// A handler is registered under HKEY_CLASSES_ROOT\&lt;scheme&gt; with a "URL Protocol" value;
    /// HKCR is the merged HKLM+HKCU view, so this also sees per-user (incl. packaged) handlers.
    /// ShellExecute doesn't throw for an unregistered protocol - Windows either silently no-ops
    /// or pops its own "how do you want to open this?" dialog - so a catch can't detect a missing
    /// handler; the caller checks this up front instead.
    /// </summary>
    public static bool IsScriptManagerInstalled()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(ProtocolScheme);
            return key?.GetValue("URL Protocol") is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

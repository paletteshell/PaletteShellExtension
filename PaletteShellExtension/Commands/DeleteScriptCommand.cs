using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;
using System.IO;

namespace PaletteShellExtension.Commands;

/// <summary>
/// Deletes a script (to the Recycle Bin) after a confirmation dialog, then invokes
/// <paramref name="onDeleted"/> so the owning page can refresh its list. Modeled on the
/// confirm-then-run pattern used for destructive scripts in <see cref="RunScriptCommand"/>.
/// </summary>
internal sealed partial class DeleteScriptCommand(string path, Action onDeleted) : InvokableCommand
{
    public override string Name => "Delete script";
    public override IconInfo Icon => new(""); // Delete

    public override CommandResult Invoke()
    {
        var scriptName = Path.GetFileName(path);
        return CommandResult.Confirm(new ConfirmationArgs
        {
            Title = $"Delete {scriptName}?",
            Description = "Moves the script to the Recycle Bin.",
            PrimaryCommand = new CallbackCommand("Delete", DeleteNow),
            IsPrimaryCommandCritical = true,
        });
    }

    private CommandResult DeleteNow()
    {
        try
        {
            if (!RecycleBin.TryDelete(path))
            {
                return CommandResult.ShowToast("Couldn't delete the script");
            }
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Couldn't delete: {ex.Message}");
        }

        onDeleted();
        return CommandResult.ShowToast("Script deleted");
    }
}

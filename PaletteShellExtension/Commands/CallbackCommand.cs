using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace PaletteShellExtension.Commands;

/// <summary>
/// An <see cref="InvokableCommand"/> that runs a supplied callback when invoked. Used as the
/// confirmed action behind <see cref="CommandResult.Confirm"/> so a destructive script's real
/// execution only happens after the user accepts the confirmation dialog.
/// </summary>
internal sealed partial class CallbackCommand(string name, Func<CommandResult> action) : InvokableCommand
{
    public override string Name => name;

    public override CommandResult Invoke() => action();
}

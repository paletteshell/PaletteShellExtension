using System;
using System.Threading;
using Windows.ApplicationModel.DataTransfer;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Native WinRT clipboard write for the Windows-only packaged extension.
///
/// The WinRT clipboard APIs (<see cref="Clipboard.SetContent"/> / <see cref="Clipboard.Flush"/>)
/// must be called on an STA thread. Our callers run on the threadpool (script output is dispatched
/// from an async continuation, command Invoke can land anywhere), which is MTA — calling directly
/// there throws RPC_E_WRONG_THREAD and the clipboard is never set. So the write is marshalled onto
/// a dedicated STA thread here. Callers keep their own try/catch; clipboard access can fail
/// transiently and any STA-thread exception is rethrown to them.
/// </summary>
internal static class ClipboardHelper
{
    internal static void SetText(string text)
    {
        RunOnSta(() =>
        {
            var data = new DataPackage();
            data.SetText(text ?? "");
            Clipboard.SetContent(data);
            Clipboard.Flush(); // persist so the content survives after this process exits
        });
    }

    // Run the action on a fresh STA thread and block until it finishes, surfacing any exception.
    private static void RunOnSta(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return;
        }

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw failure;
        }
    }
}

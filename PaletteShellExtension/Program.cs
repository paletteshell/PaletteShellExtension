// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using PaletteShellExtension.Classes;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PaletteShellExtension;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        // Last-chance visibility: nothing can stop the process from dying on an unhandled
        // exception, but writing it to the file log first means a crash report from the
        // Store can be matched to an actual exception and stack instead of guessed at.
        // Registered before anything else so even activation-time failures are captured.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error("FATAL unhandled exception", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        try
        {
            if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
            {
                global::Shmuelie.WinRTServer.ComServer server = new();

                ManualResetEvent extensionDisposedEvent = new(false);

                // We are instantiating an extension instance once above, and returning it every time the callback in RegisterExtension below is called.
                // This makes sure that only one instance of SampleExtension is alive, which is returned every time the host asks for the IExtension object.
                // If you want to instantiate a new instance each time the host asks, create the new instance inside the delegate.
                PaletteShellExtension extensionInstance = new(extensionDisposedEvent);
                server.RegisterClass<PaletteShellExtension, IExtension>(() => extensionInstance);
                server.Start();

                // This will make the main thread wait until the event is signalled by the extension class.
                // Since we have single instance of the extension object, we exit as soon as it is disposed.
                extensionDisposedEvent.WaitOne();
                server.Stop();
                server.UnsafeDispose();
            }
            else
            {
                Console.WriteLine("Not being launched as a Extension... exiting.");
            }
        }
        catch (Exception ex)
        {
            // The AppDomain handler above only sees exceptions that reach the runtime
            // unhandled; catching here too guarantees the log line even if a future
            // handler registration is reordered. The rethrow keeps the crash visible
            // to Windows Error Reporting rather than masking a broken activation.
            Log.Error("FATAL: extension host startup failed", ex);
            throw;
        }
    }
}

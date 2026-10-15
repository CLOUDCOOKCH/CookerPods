using System.Windows;
using CookerPods.Diagnostic.Core;

namespace CookerPods.Diagnostic.Windows;

internal static class EntryPoint
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--diagnostic"))
            return DiagnosticProgram.RunAsync(args.Where(a => a != "--diagnostic").ToArray()).GetAwaiter().GetResult();
        if (args.Length == 1 && args[0] == "--help")
        {
            Console.WriteLine("CookerPods — Windows battery monitor\nOpen without arguments for the monitor window.\n--diagnostic [--seconds 10..120] [--all-paired] [--no-wait] runs the read-only diagnostic.\nNo device controls, driver changes, network listener, or automatic uploads.");
            return 0;
        }
        if (args.Length == 2 && args[0] == "--ui-smoke") return MonitorUiSmoke.Run(args[1]);
        if (args.Length != 0) return 2;
        using var session = new LiveMonitorSession();
        using var radio = new MonitorRadio(session);
        var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        application.DispatcherUnhandledException += (_, error) =>
        {
            MessageBox.Show("CookerPods encountered an unexpected error. Close the window and restart the app.",
                "CookerPods", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
            application.Shutdown(1);
        };
        return application.Run(new MonitorWindow(session, radio));
    }
}

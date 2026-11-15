using System.Windows;
using System.Windows.Threading;

namespace ThunderClient;

public partial class App : Application
{
    private bool _smokeTest;
    protected override void OnStartup(StartupEventArgs e)
    {
        _smokeTest = e.Args.Any(x => x.Equals("--ui-smoke-test", StringComparison.OrdinalIgnoreCase));
        StartupDiagnostics.Trace("OnStartup: gestarteter Prozess, smoke=" + _smokeTest);
        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                StartupDiagnostics.Record("Nicht behandelter Hintergrundfehler", exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupDiagnostics.Record("Nicht beobachteter Task", args.Exception);
            args.SetObserved();
        };

        try
        {
            base.OnStartup(e);
            StartupDiagnostics.Trace("OnStartup: beginne MainWindow-Konstruktion");
            var window = new MainWindow(_smokeTest);
            StartupDiagnostics.Trace("OnStartup: MainWindow konstruiert");
            MainWindow = window;
            if (_smokeTest)
            {
                window.ContentRendered += (_, _) =>
                {
                    window.VerifyPagesForSmokeTest();
                    StartupDiagnostics.Trace("Alle sieben WPF-Bereiche gerendert und Navigation geprüft.");
                    window.Close();
                    Shutdown(0);
                };
            }
            StartupDiagnostics.Trace("OnStartup: vor Show");
            window.Show();
            StartupDiagnostics.Trace("OnStartup: Show abgeschlossen");
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Record("Fensterstart", exception);
            if (_smokeTest) { Shutdown(1); return; }
            try
            {
                MessageBox.Show(
                    "Die Oberfläche konnte nicht gestartet werden.\n\n" +
                    exception.GetType().Name + ": " + exception.Message +
                    "\n\nDiagnose: " + StartupDiagnostics.LogPath,
                    "Thunder Compute Client – Startfehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { /* Auch in einer defekten Desktop-Sitzung sauber beenden. */ }
            Shutdown(1);
        }
    }

    private void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.Record("WPF", e.Exception);
        if (_smokeTest) { e.Handled = true; Shutdown(1); return; }
        try
        {
            MessageBox.Show(
                e.Exception.GetType().Name + ": " + e.Exception.Message +
                "\n\nDiagnose: " + StartupDiagnostics.LogPath,
                "Thunder Compute Client – Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
        // In inkonsistentem UI-Zustand nicht still weiterlaufen.
        e.Handled = true;
        Shutdown(1);
    }
}

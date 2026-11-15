using System.Windows;
using System.Windows.Threading;

namespace ThunderClient;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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
            var smokeTest = e.Args.Any(x => x.Equals("--ui-smoke-test", StringComparison.OrdinalIgnoreCase));
            var window = new MainWindow(smokeTest);
            MainWindow = window;
            if (smokeTest)
            {
                window.ContentRendered += (_, _) =>
                {
                    StartupDiagnostics.Trace("WPF-Fenster vollständig gerendert.");
                    window.Close();
                    Shutdown(0);
                };
            }
            window.Show();
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Record("Fensterstart", exception);
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

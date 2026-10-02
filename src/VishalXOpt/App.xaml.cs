using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace VishalXOpt;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogFatal(args.ExceptionObject as Exception);

        var window = new MainWindow();
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatal(e.Exception);
        MessageBox.Show(
            $"Vishal X Opt hit an unexpected error and needs to continue carefully:\n\n{e.Exception.Message}",
            "Vishal X Opt", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true; // keep the app alive - a failed tweak should never take the whole UI down
    }

    private static void LogFatal(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VishalXOpt", "Logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(
                Path.Combine(folder, "crash.log"),
                $"{DateTime.Now:u}  {ex}\n\n");
        }
        catch
        {
            // logging must never itself throw
        }
    }
}

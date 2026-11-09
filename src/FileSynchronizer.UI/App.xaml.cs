using System.IO;
using System.Windows;

namespace FileSynchronizer.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileSynchronizer",
        "app.log");

    public static void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("App OnStartup beginning...");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log($"Unhandled AppDomain Exception: {args.ExceptionObject}");
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            Log($"Unhandled Dispatcher Exception: {args.Exception}");
            MessageBox.Show($"Wystąpił błąd:\n{args.Exception}", "Błąd FileSynchronizer", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);
        Log("App OnStartup base completed.");
    }
}

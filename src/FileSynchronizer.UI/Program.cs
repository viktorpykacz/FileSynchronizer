using System.IO;
using System.Windows;

namespace FileSynchronizer.UI;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileSynchronizer");
        var logFile = Path.Combine(appData, "app_startup.log");

        try
        {
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            File.WriteAllText(logFile, $"[1] Application starting at {DateTime.Now}\n");

            var app = new App();
            File.AppendAllText(logFile, "[2] App instance created\n");

            app.InitializeComponent();
            File.AppendAllText(logFile, "[3] App InitializeComponent completed\n");

            var mainWindow = new MainWindow();
            File.AppendAllText(logFile, "[4] MainWindow instantiated\n");

            mainWindow.Show();
            mainWindow.Activate();
            File.AppendAllText(logFile, "[5] MainWindow shown and activated\n");

            app.Run(mainWindow);
            File.AppendAllText(logFile, "[6] app.Run exited cleanly\n");
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(logFile, $"[FATAL ERROR] {ex}\n");
            }
            catch { }

            MessageBox.Show($"Błąd podczas uruchamiania FileSynchronizer:\n\n{ex.Message}\n\nSzczegóły w: {logFile}",
                "Błąd FileSynchronizer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

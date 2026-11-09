using System.ComponentModel;
using System.Windows;
using FileSynchronizer.UI.Services;
using FileSynchronizer.UI.ViewModels;

namespace FileSynchronizer.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly TrayIconService _trayService;
    private bool _isExitingFromTray;
    private bool _hasShownMinimizeNotice;

    public MainWindow()
    {
        InitializeComponent();

        _trayService = new TrayIconService(
            onRestoreRequested: RestoreWindow,
            onExitRequested: ExitApplicationFully);

        Loaded += (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.NotificationRequested += (sender, args) =>
                {
                    _trayService.ShowNotification(args.Title, args.Message, args.IsError);
                };
            }
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExitingFromTray)
        {
            // Do not quit - minimize to system tray
            e.Cancel = true;
            Hide();

            if (!_hasShownMinimizeNotice)
            {
                _hasShownMinimizeNotice = true;
                _trayService.ShowNotification(
                    "FileSynchronizer działa w tle 🔄",
                    "Aplikacja monitoruje podłączanie pendrive'ów w zasobniku systemowym. Kliknij ikonkę w zasobniku, aby otworzyć okno.");
            }
        }
        else
        {
            _trayService.Dispose();
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
            base.OnClosing(e);
        }
    }

    private void RestoreWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Focus();
    }

    private void ExitApplicationFully()
    {
        _isExitingFromTray = true;
        _trayService.Dispose();
        Close();
        Application.Current.Shutdown();
    }
}
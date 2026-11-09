using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace FileSynchronizer.UI.Services;

/// <summary>
/// Native WPF implementation of Windows System Tray Icon using Shell_NotifyIcon Win32 API.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1024;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;

    private const int NIIF_INFO = 0x00000001;
    private const int NIIF_WARNING = 0x00000002;
    private const int NIIF_ERROR = 0x00000003;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;

    private readonly Action _onRestoreRequested;
    private readonly Action _onExitRequested;
    private readonly HwndSource _hwndSource;
    private readonly IntPtr _hIcon;
    private readonly ContextMenu _contextMenu;
    private bool _isDisposed;

    public TrayIconService(Action onRestoreRequested, Action onExitRequested)
    {
        _onRestoreRequested = onRestoreRequested;
        _onExitRequested = onExitRequested;

        // Create a hidden message window to receive tray messages
        var parameters = new HwndSourceParameters("FileSyncTrayMessageSink")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0
        };
        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        _hIcon = LoadCustomAppIcon();

        // Setup native WPF Context Menu for Tray
        _contextMenu = new ContextMenu();

        var openItem = new MenuItem
        {
            Header = "📂 Otwórz FileSynchronizer",
            FontWeight = FontWeights.Bold
        };
        openItem.Click += (s, e) => _onRestoreRequested();
        _contextMenu.Items.Add(openItem);

        var statusItem = new MenuItem
        {
            Header = "⚡ Nasłuchiwanie USB: Aktywne",
            IsEnabled = false
        };
        _contextMenu.Items.Add(statusItem);

        _contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem
        {
            Header = "🚪 Zamknij całkowicie program"
        };
        exitItem.Click += (s, e) => _onExitRequested();
        _contextMenu.Items.Add(exitItem);

        // Register tray icon with Windows Shell
        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwndSource.Handle,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "FileSynchronizer (działa w tle)"
        };

        Shell_NotifyIcon(NIM_ADD, ref data);
    }

    public void ShowNotification(string title, string message, bool isError = false)
    {
        if (_isDisposed) return;

        try
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwndSource.Handle,
                uID = 1001,
                uFlags = NIF_INFO,
                szInfoTitle = title,
                szInfo = message,
                dwInfoFlags = isError ? NIIF_ERROR : NIIF_INFO
            };

            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }
        catch { }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int mouseMsg = lParam.ToInt32();
            if (mouseMsg == WM_LBUTTONDBLCLK || mouseMsg == WM_LBUTTONUP)
            {
                _onRestoreRequested();
                handled = true;
            }
            else if (mouseMsg == WM_RBUTTONUP)
            {
                SetForegroundWindow(_hwndSource.Handle);
                _contextMenu.IsOpen = true;
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwndSource.Handle,
            uID = 1001
        };
        Shell_NotifyIcon(NIM_DELETE, ref data);

        _hwndSource.RemoveHook(WndProc);
        _hwndSource.Dispose();
    }

    private static IntPtr LoadCustomAppIcon()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var icoPath = System.IO.Path.Combine(baseDir, "Assets", "app.ico");
            if (System.IO.File.Exists(icoPath))
            {
                var hIcon = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                if (hIcon != IntPtr.Zero)
                {
                    return hIcon;
                }
            }
        }
        catch { }

        return LoadIcon(IntPtr.Zero, (IntPtr)32512);
    }
}

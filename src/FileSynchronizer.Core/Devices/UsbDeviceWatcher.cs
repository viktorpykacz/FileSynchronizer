using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Devices;

/// <summary>
/// Monitors USB and drive connect / disconnect events using Win32 API and polling with WMI event support.
/// </summary>
public sealed class UsbDeviceWatcher : IUsbDeviceWatcher
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int nFileSystemNameSize);

    private readonly object _lock = new();
    private readonly Dictionary<string, DriveDeviceInfo> _activeDrives = new(StringComparer.OrdinalIgnoreCase);
    private ManagementEventWatcher? _wmiInsertWatcher;
    private ManagementEventWatcher? _wmiRemoveWatcher;
    private Timer? _pollingTimer;
    private bool _isRunning;

    public event EventHandler<DriveDeviceInfo>? DriveConnected;
    public event EventHandler<string>? DriveDisconnected;

    public UsbDeviceWatcher()
    {
        RefreshDrivesInternal(notify: false);
    }

    public IReadOnlyList<DriveDeviceInfo> GetConnectedDrives()
    {
        lock (_lock)
        {
            return _activeDrives.Values.ToList();
        }
    }

    public IReadOnlyList<DriveDeviceInfo> GetRemovableDrives()
    {
        lock (_lock)
        {
            return _activeDrives.Values
                .Where(d => d.DriveType == DriveType.Removable)
                .ToList();
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;

            // Start periodic heartbeat timer (every 2 seconds) - ultra lightweight and non-blocking
            _pollingTimer = new Timer(_ => RefreshDrivesInternal(notify: true), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            // Initialize WMI watchers in background to prevent UI freeze
            Task.Run(() =>
            {
                try
                {
                    var insertQuery = new WqlEventQuery("__InstanceCreationEvent", TimeSpan.FromSeconds(1), "TargetInstance ISA 'Win32_LogicalDisk'");
                    var insertWatcher = new ManagementEventWatcher(insertQuery);
                    insertWatcher.EventArrived += (s, e) => OnWmiDriveChange();
                    insertWatcher.Start();

                    var removeQuery = new WqlEventQuery("__InstanceDeletionEvent", TimeSpan.FromSeconds(1), "TargetInstance ISA 'Win32_LogicalDisk'");
                    var removeWatcher = new ManagementEventWatcher(removeQuery);
                    removeWatcher.EventArrived += (s, e) => OnWmiDriveChange();
                    removeWatcher.Start();

                    lock (_lock)
                    {
                        if (_isRunning)
                        {
                            _wmiInsertWatcher = insertWatcher;
                            _wmiRemoveWatcher = removeWatcher;
                        }
                        else
                        {
                            insertWatcher.Dispose();
                            removeWatcher.Dispose();
                        }
                    }
                }
                catch
                {
                    // WMI not available; fallback timer is already running
                }
            });
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;

            try
            {
                _wmiInsertWatcher?.Stop();
                _wmiInsertWatcher?.Dispose();
                _wmiInsertWatcher = null;

                _wmiRemoveWatcher?.Stop();
                _wmiRemoveWatcher?.Dispose();
                _wmiRemoveWatcher = null;
            }
            catch { }

            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }
    }

    private void OnWmiDriveChange()
    {
        // Small delay to allow Windows to mount volume completely
        Task.Delay(500).ContinueWith(_ => RefreshDrivesInternal(notify: true));
    }

    private void RefreshDrivesInternal(bool notify)
    {
        List<DriveDeviceInfo> addedDrives = new();
        List<string> removedDrives = new();

        lock (_lock)
        {
            var currentDriveInfos = GetSystemDrives();
            var currentDriveMap = currentDriveInfos.ToDictionary(d => d.RootPath, d => d, StringComparer.OrdinalIgnoreCase);

            // Check for added drives
            foreach (var kvp in currentDriveMap)
            {
                if (!_activeDrives.ContainsKey(kvp.Key))
                {
                    _activeDrives[kvp.Key] = kvp.Value;
                    addedDrives.Add(kvp.Value);
                }
                else
                {
                    // Update metadata
                    _activeDrives[kvp.Key] = kvp.Value;
                }
            }

            // Check for removed drives
            foreach (var existingKey in _activeDrives.Keys.ToList())
            {
                if (!currentDriveMap.ContainsKey(existingKey))
                {
                    _activeDrives.Remove(existingKey);
                    removedDrives.Add(existingKey);
                }
            }
        }

        if (notify)
        {
            foreach (var drive in addedDrives)
            {
                try
                {
                    DriveConnected?.Invoke(this, drive);
                }
                catch { }
            }

            foreach (var rootPath in removedDrives)
            {
                try
                {
                    DriveDisconnected?.Invoke(this, rootPath);
                }
                catch { }
            }
        }
    }

    private static List<DriveDeviceInfo> GetSystemDrives()
    {
        var result = new List<DriveDeviceInfo>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                string volumeSerial = string.Empty;
                string volumeLabel = drive.VolumeLabel;
                string fileSystem = drive.DriveFormat;

                try
                {
                    var volName = new StringBuilder(261);
                    var fsName = new StringBuilder(261);
                    if (GetVolumeInformation(drive.RootDirectory.FullName, volName, volName.Capacity, out uint serial, out _, out _, fsName, fsName.Capacity))
                    {
                        volumeSerial = serial.ToString("X8");
                        if (string.IsNullOrEmpty(volumeLabel))
                        {
                            volumeLabel = volName.ToString();
                        }
                        if (string.IsNullOrEmpty(fileSystem))
                        {
                            fileSystem = fsName.ToString();
                        }
                    }
                }
                catch
                {
                    // Fallback to basic DriveInfo
                }

                result.Add(new DriveDeviceInfo
                {
                    RootPath = drive.RootDirectory.FullName,
                    VolumeName = volumeLabel,
                    VolumeSerialNumber = volumeSerial,
                    DriveType = drive.DriveType,
                    DriveFormat = fileSystem,
                    TotalSize = drive.TotalSize,
                    AvailableFreeSpace = drive.AvailableFreeSpace,
                    IsReady = true
                });
            }
        }
        catch
        {
            // Ignore drive enumeration errors
        }

        return result;
    }

    public void Dispose()
    {
        Stop();
    }
}

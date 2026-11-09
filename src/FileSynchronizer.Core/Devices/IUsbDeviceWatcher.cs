using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Devices;

public interface IUsbDeviceWatcher : IDisposable
{
    /// <summary>
    /// Raised when a new drive / USB storage device is connected.
    /// </summary>
    event EventHandler<DriveDeviceInfo>? DriveConnected;

    /// <summary>
    /// Raised when a drive / USB storage device is disconnected.
    /// </summary>
    event EventHandler<string>? DriveDisconnected;

    /// <summary>
    /// Gets all currently available storage drives.
    /// </summary>
    IReadOnlyList<DriveDeviceInfo> GetConnectedDrives();

    /// <summary>
    /// Gets only removable / USB storage drives.
    /// </summary>
    IReadOnlyList<DriveDeviceInfo> GetRemovableDrives();

    /// <summary>
    /// Starts watching for drive connect/disconnect events.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops watching.
    /// </summary>
    void Stop();
}

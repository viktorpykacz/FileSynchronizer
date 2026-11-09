using FileSynchronizer.Core.Devices;
using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Services;

public class UsbAutoSyncEventArgs : EventArgs
{
    public required SyncJob Job { get; init; }
    public required DriveDeviceInfo Drive { get; init; }
    public SyncPlan? Plan { get; set; }
    public SyncResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IUsbAutoSyncService : IDisposable
{
    event EventHandler<UsbAutoSyncEventArgs>? AutoSyncStarted;
    event EventHandler<UsbAutoSyncEventArgs>? AutoSyncCompleted;
    event EventHandler<UsbAutoSyncEventArgs>? AutoSyncFailed;

    void Start();
    void Stop();
}

using FileSynchronizer.Core.Devices;
using FileSynchronizer.Core.Engine;
using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Services;

/// <summary>
/// Background service that listens for USB insertions and triggers matching AutoSync jobs.
/// </summary>
public sealed class UsbAutoSyncService : IUsbAutoSyncService
{
    private readonly IUsbDeviceWatcher _usbWatcher;
    private readonly IJobManager _jobManager;
    private readonly ISyncEngine _syncEngine;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
    private bool _isRunning;

    public event EventHandler<UsbAutoSyncEventArgs>? AutoSyncStarted;
    public event EventHandler<UsbAutoSyncEventArgs>? AutoSyncCompleted;
    public event EventHandler<UsbAutoSyncEventArgs>? AutoSyncFailed;

    public UsbAutoSyncService(
        IUsbDeviceWatcher usbWatcher,
        IJobManager jobManager,
        ISyncEngine syncEngine)
    {
        _usbWatcher = usbWatcher;
        _jobManager = jobManager;
        _syncEngine = syncEngine;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        _usbWatcher.DriveConnected += OnDriveConnected;
        _usbWatcher.Start();
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        _usbWatcher.DriveConnected -= OnDriveConnected;
    }

    private void OnDriveConnected(object? sender, DriveDeviceInfo drive)
    {
        _ = Task.Run(async () =>
        {
            await _syncSemaphore.WaitAsync();
            try
            {
                var jobs = _jobManager.GetAllJobs()
                    .Where(j => j.IsEnabled && j.AutoSyncOnUsbConnect)
                    .ToList();

                foreach (var job in jobs)
                {
                    if (IsJobMatchingDrive(job, drive))
                    {
                        await RunAutoSyncForJobAsync(job, drive);
                    }
                }
            }
            finally
            {
                _syncSemaphore.Release();
            }
        });
    }

    private static bool IsJobMatchingDrive(SyncJob job, DriveDeviceInfo drive)
    {
        // Check explicit serial number match
        if (!string.IsNullOrWhiteSpace(job.TargetUsbVolumeSerial))
        {
            if (string.Equals(job.TargetUsbVolumeSerial.Replace("-", ""), drive.VolumeSerialNumber.Replace("-", ""), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check explicit volume label match
        if (!string.IsNullOrWhiteSpace(job.TargetUsbVolumeLabel))
        {
            if (string.Equals(job.TargetUsbVolumeLabel, drive.VolumeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check if LeftPath or RightPath contains this USB's serial or label
        if (UsbPathResolver.IsUsbDynamicPath(job.LeftPath) || UsbPathResolver.IsUsbDynamicPath(job.RightPath))
        {
            var singleDriveList = new[] { drive };
            var leftOk = !UsbPathResolver.IsUsbDynamicPath(job.LeftPath) || UsbPathResolver.ResolvePath(job.LeftPath, singleDriveList).Success;
            var rightOk = !UsbPathResolver.IsUsbDynamicPath(job.RightPath) || UsbPathResolver.ResolvePath(job.RightPath, singleDriveList).Success;

            if (leftOk && rightOk)
            {
                return true;
            }
        }

        return false;
    }

    private async Task RunAutoSyncForJobAsync(SyncJob job, DriveDeviceInfo drive)
    {
        var eventArgs = new UsbAutoSyncEventArgs
        {
            Job = job,
            Drive = drive
        };

        try
        {
            AutoSyncStarted?.Invoke(this, eventArgs);

            // 1. Analyze
            var plan = await _syncEngine.AnalyzeAsync(job);
            eventArgs.Plan = plan;

            // 2. Execute
            var result = await _syncEngine.ExecuteAsync(job, plan);
            eventArgs.Result = result;

            await _jobManager.UpdateJobAsync(job);

            AutoSyncCompleted?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            eventArgs.ErrorMessage = ex.Message;
            AutoSyncFailed?.Invoke(this, eventArgs);
        }
    }

    public void Dispose()
    {
        Stop();
        _syncSemaphore.Dispose();
    }
}

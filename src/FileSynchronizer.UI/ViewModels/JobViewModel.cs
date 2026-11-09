using FileSynchronizer.Core.Models;
using FileSynchronizer.UI.Common;

namespace FileSynchronizer.UI.ViewModels;

public class JobViewModel : ObservableObject
{
    public SyncJob Model { get; }

    public Guid Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name != value)
            {
                Model.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string LeftPath
    {
        get => Model.LeftPath;
        set
        {
            if (Model.LeftPath != value)
            {
                Model.LeftPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LeftDisplayPath));
                OnPropertyChanged(nameof(IsLeftUsb));
                OnPropertyChanged(nameof(LeftFriendlyDisplay));
            }
        }
    }

    public string RightPath
    {
        get => Model.RightPath;
        set
        {
            if (Model.RightPath != value)
            {
                Model.RightPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RightDisplayPath));
                OnPropertyChanged(nameof(IsRightUsb));
                OnPropertyChanged(nameof(RightFriendlyDisplay));
            }
        }
    }

    public string LeftDisplayPath => string.IsNullOrWhiteSpace(Model.LeftPath) ? "Wybierz folder źródłowy..." : Model.LeftPath;
    public string RightDisplayPath => string.IsNullOrWhiteSpace(Model.RightPath) ? "Wybierz folder docelowy / USB..." : Model.RightPath;

    public bool IsLeftUsb => FileSynchronizer.Core.Devices.UsbPathResolver.IsUsbDynamicPath(Model.LeftPath);
    public bool IsRightUsb => FileSynchronizer.Core.Devices.UsbPathResolver.IsUsbDynamicPath(Model.RightPath);

    private IReadOnlyList<DriveDeviceInfo> _cachedDrives = Array.Empty<DriveDeviceInfo>();

    public string LeftFriendlyDisplay => IsLeftUsb
        ? FileSynchronizer.Core.Devices.UsbPathResolver.GetFriendlyDisplayPath(Model.LeftPath, _cachedDrives)
        : Model.LeftPath;

    public string RightFriendlyDisplay => IsRightUsb
        ? FileSynchronizer.Core.Devices.UsbPathResolver.GetFriendlyDisplayPath(Model.RightPath, _cachedDrives)
        : Model.RightPath;

    public void RefreshDisplays(IReadOnlyList<DriveDeviceInfo> drives)
    {
        _cachedDrives = drives;
        OnPropertyChanged(nameof(IsLeftUsb));
        OnPropertyChanged(nameof(IsRightUsb));
        OnPropertyChanged(nameof(LeftFriendlyDisplay));
        OnPropertyChanged(nameof(RightFriendlyDisplay));
        OnPropertyChanged(nameof(LeftDisplayPath));
        OnPropertyChanged(nameof(RightDisplayPath));
    }

    public SyncDirection Direction
    {
        get => Model.Direction;
        set
        {
            if (Model.Direction != value)
            {
                Model.Direction = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOneWayBackup));
                OnPropertyChanged(nameof(IsTwoWaySync));
                OnPropertyChanged(nameof(DirectionName));
                OnPropertyChanged(nameof(DirectionIcon));
            }
        }
    }

    public bool IsOneWayBackup
    {
        get => Model.Direction == SyncDirection.OneWayBackup;
        set
        {
            if (value) Direction = SyncDirection.OneWayBackup;
        }
    }

    public bool IsTwoWaySync
    {
        get => Model.Direction == SyncDirection.TwoWaySync;
        set
        {
            if (value) Direction = SyncDirection.TwoWaySync;
        }
    }

    public string DirectionName => Model.Direction == SyncDirection.OneWayBackup ? "Kopia 1-kierunkowa (Backup)" : "Synchronizacja 2-kierunkowa (Obustronna)";
    public string DirectionIcon => Model.Direction == SyncDirection.OneWayBackup ? "➡️" : "🔄";

    public bool AutoSyncOnUsbConnect
    {
        get => Model.AutoSyncOnUsbConnect;
        set
        {
            if (Model.AutoSyncOnUsbConnect != value)
            {
                Model.AutoSyncOnUsbConnect = value;
                OnPropertyChanged();
            }
        }
    }

    public bool DeleteOrphanFilesOnTarget
    {
        get => Model.DeleteOrphanFilesOnTarget;
        set
        {
            if (Model.DeleteOrphanFilesOnTarget != value)
            {
                Model.DeleteOrphanFilesOnTarget = value;
                OnPropertyChanged();
            }
        }
    }

    public string? TargetUsbVolumeSerial
    {
        get => Model.TargetUsbVolumeSerial;
        set
        {
            if (Model.TargetUsbVolumeSerial != value)
            {
                Model.TargetUsbVolumeSerial = value;
                OnPropertyChanged();
            }
        }
    }

    public string? TargetUsbVolumeLabel
    {
        get => Model.TargetUsbVolumeLabel;
        set
        {
            if (Model.TargetUsbVolumeLabel != value)
            {
                Model.TargetUsbVolumeLabel = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? LastSyncTimeUtc
    {
        get => Model.LastSyncTimeUtc;
        set
        {
            if (Model.LastSyncTimeUtc != value)
            {
                Model.LastSyncTimeUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastSyncTimeFormatted));
            }
        }
    }

    public string LastSyncTimeFormatted => Model.LastSyncTimeUtc.HasValue
        ? Model.LastSyncTimeUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
        : "Nigdy";

    public string LastSyncStatus
    {
        get => Model.LastSyncStatus ?? "Gotowy do analizy";
        set
        {
            if (Model.LastSyncStatus != value)
            {
                Model.LastSyncStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public JobViewModel(SyncJob model)
    {
        Model = model;
    }
}

using FileSynchronizer.Core.Devices;
using FileSynchronizer.Core.Models;
using FileSynchronizer.UI.Common;

namespace FileSynchronizer.UI.ViewModels;

public class DriveItemViewModel : ObservableObject
{
    public DriveDeviceInfo Drive { get; }

    public string RootPath => Drive.RootPath;
    public string VolumeName => string.IsNullOrWhiteSpace(Drive.VolumeName) ? "Pendrive" : Drive.VolumeName;
    public string VolumeSerialNumber => Drive.VolumeSerialNumber;
    public string DriveFormat => Drive.DriveFormat;
    public string DisplayName => Drive.DisplayName;
    public string DynamicUsbUri => UsbPathResolver.CreateUsbUri(Drive);

    public DriveItemViewModel(DriveDeviceInfo drive)
    {
        Drive = drive;
    }
}

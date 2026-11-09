using System.IO;
using FileSynchronizer.Core.Devices;
using FileSynchronizer.Core.Models;
using Xunit;

namespace FileSynchronizer.Core.Tests;

public class UsbPathResolverTests
{
    private readonly List<DriveDeviceInfo> _fakeDrives = new()
    {
        new DriveDeviceInfo
        {
            RootPath = @"E:\",
            VolumeName = "MY_PENDRIVE",
            VolumeSerialNumber = "A1B2C3D4",
            DriveType = DriveType.Removable,
            DriveFormat = "FAT32",
            TotalSize = 32_000_000_000,
            AvailableFreeSpace = 16_000_000_000,
            IsReady = true
        },
        new DriveDeviceInfo
        {
            RootPath = @"C:\",
            VolumeName = "System",
            VolumeSerialNumber = "99887766",
            DriveType = DriveType.Fixed,
            DriveFormat = "NTFS",
            TotalSize = 500_000_000_000,
            AvailableFreeSpace = 200_000_000_000,
            IsReady = true
        }
    };

    [Fact]
    public void ResolvePath_StandardPath_ReturnsExactPath()
    {
        var path = @"C:\Projects\TestFolder";
        var (success, resolved, error) = UsbPathResolver.ResolvePath(path, _fakeDrives);

        Assert.True(success);
        Assert.Equal(Path.GetFullPath(path), resolved);
        Assert.Null(error);
    }

    [Fact]
    public void ResolvePath_UsbBySerialNumber_ResolvesCorrectly()
    {
        var path = "usb://SERIAL:A1B2C3D4/Backups/Work";
        var (success, resolved, error) = UsbPathResolver.ResolvePath(path, _fakeDrives);

        Assert.True(success);
        Assert.Equal(@"E:\Backups/Work", resolved);
        Assert.Null(error);
    }

    [Fact]
    public void ResolvePath_UsbByLabel_ResolvesCorrectly()
    {
        var path = "usb://LABEL:MY_PENDRIVE/Photos";
        var (success, resolved, error) = UsbPathResolver.ResolvePath(path, _fakeDrives);

        Assert.True(success);
        Assert.Equal(@"E:\Photos", resolved);
        Assert.Null(error);
    }

    [Fact]
    public void ResolvePath_MissingUsbDrive_ReturnsError()
    {
        var path = "usb://SERIAL:UNKNOWN999/Backups";
        var (success, resolved, error) = UsbPathResolver.ResolvePath(path, _fakeDrives);

        Assert.False(success);
        Assert.Contains("Nie znaleziono", error);
    }

    [Fact]
    public void CreateUsbUri_ProducesValidUri()
    {
        var drive = _fakeDrives[0];
        var uri = UsbPathResolver.CreateUsbUri(drive, "Projects/Backup");

        Assert.Equal("usb://SERIAL:A1B2C3D4/Projects/Backup", uri);
    }
}

namespace FileSynchronizer.Core.Models;

/// <summary>
/// Information about a detected storage drive / USB device.
/// </summary>
public record DriveDeviceInfo
{
    public required string RootPath { get; init; } // e.g. "E:\"
    public required string VolumeName { get; init; } // e.g. "KINGSTON"
    public required string VolumeSerialNumber { get; init; } // e.g. "A1B2-C3D4"
    public required DriveType DriveType { get; init; } // Removable, Fixed, etc.
    public required string DriveFormat { get; init; } // NTFS, FAT32, exFAT
    public long TotalSize { get; init; }
    public long AvailableFreeSpace { get; init; }
    public bool IsReady { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(VolumeName)
        ? $"{RootPath} ({FormatBytes(AvailableFreeSpace)} wolne z {FormatBytes(TotalSize)})"
        : $"{VolumeName} ({RootPath}) [{FormatBytes(AvailableFreeSpace)} wolne]";

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}

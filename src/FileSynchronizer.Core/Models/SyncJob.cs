using System.Text.Json.Serialization;

namespace FileSynchronizer.Core.Models;

/// <summary>
/// Configuration for a synchronization job.
/// </summary>
public class SyncJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nowe zadanie";
    public string? Description { get; set; }

    /// <summary>
    /// Source / Left directory path.
    /// Can be a regular path (e.g. C:\Data) or USB dynamic path (e.g. usb://[SerialNumber]/Data or usb://[Label]/Data).
    /// </summary>
    public string LeftPath { get; set; } = string.Empty;

    /// <summary>
    /// Destination / Right directory path.
    /// Can be a regular path or USB dynamic path.
    /// </summary>
    public string RightPath { get; set; } = string.Empty;

    /// <summary>
    /// Direction of sync (Backup vs TwoWay).
    /// </summary>
    public SyncDirection Direction { get; set; } = SyncDirection.OneWayBackup;

    /// <summary>
    /// Comparison method (SizeAndDate, SizeAndDateWithHashFallback, HashOnly).
    /// </summary>
    public CompareMethod CompareMethod { get; set; } = CompareMethod.SizeAndDate;

    /// <summary>
    /// How to handle conflicts in TwoWaySync mode.
    /// </summary>
    public ConflictResolutionPolicy ConflictPolicy { get; set; } = ConflictResolutionPolicy.Manual;

    /// <summary>
    /// In OneWayBackup mode: if true, files deleted in source will also be deleted from destination.
    /// If false, destination files are never deleted (accumulative backup).
    /// </summary>
    public bool DeleteOrphanFilesOnTarget { get; set; } = true;

    /// <summary>
    /// If true, this job will automatically run or notify when the assigned USB device is plugged in.
    /// </summary>
    public bool AutoSyncOnUsbConnect { get; set; } = false;

    /// <summary>
    /// Volume Serial Number of the target USB device (used to reliably track device even if drive letter changes).
    /// </summary>
    public string? TargetUsbVolumeSerial { get; set; }

    /// <summary>
    /// Volume Label of the target USB device.
    /// </summary>
    public string? TargetUsbVolumeLabel { get; set; }

    /// <summary>
    /// Glob exclusion patterns (e.g. "*.tmp", ".git/*", "Thumbs.db", "~$*").
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new()
    {
        "*.tmp",
        "~$*",
        "Thumbs.db",
        "desktop.ini",
        ".sync/*",
        ".git/*",
        "$RECYCLE.BIN/*",
        "System Volume Information/*"
    };

    /// <summary>
    /// Glob inclusion patterns (empty list means include everything).
    /// </summary>
    public List<string> IncludePatterns { get; set; } = new();

    /// <summary>
    /// Last execution timestamp in UTC.
    /// </summary>
    public DateTime? LastSyncTimeUtc { get; set; }

    /// <summary>
    /// Status description from the last execution.
    /// </summary>
    public string? LastSyncStatus { get; set; }

    /// <summary>
    /// Whether the job is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Safe copy writes to a temporary file before replacing target to avoid data corruption on sudden disconnect.
    /// </summary>
    public bool UseSafeAtomicCopy { get; set; } = true;
}

namespace FileSynchronizer.Core.State;

/// <summary>
/// State of a single file in the state snapshot at the time of previous synchronization.
/// </summary>
public record FileStateEntry
{
    public required string RelativePath { get; init; }
    public long Size { get; init; }
    public DateTime LastModifiedUtc { get; init; }
    public string? Hash { get; init; }
    public bool ExistedOnLeft { get; init; }
    public bool ExistedOnRight { get; init; }
}

/// <summary>
/// Snapshot of the file system state saved after a successful sync job run.
/// </summary>
public class SyncStateSnapshot
{
    public Guid JobId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, FileStateEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

namespace FileSynchronizer.Core.Models;

/// <summary>
/// Represents a synchronization plan item for a single file or directory.
/// </summary>
public class SyncItem
{
    public required string RelativePath { get; init; }
    
    public FileMetadata? LeftFile { get; set; }
    public FileMetadata? RightFile { get; set; }
    
    public SyncActionType Action { get; set; } = SyncActionType.InSync;
    public ItemSyncStatus Status { get; set; } = ItemSyncStatus.Pending;
    
    public string? StatusDescription { get; set; }
    public string? ErrorMessage { get; set; }
    
    public long BytesTransferred { get; set; }
    public long TotalBytes => (Action == SyncActionType.CopyLeftToRight ? LeftFile?.Size : RightFile?.Size) ?? 0;

    public bool IsConflict => Action == SyncActionType.Conflict;
    public bool RequiresAction => Action is SyncActionType.CopyLeftToRight or SyncActionType.CopyRightToLeft or SyncActionType.DeleteLeft or SyncActionType.DeleteRight;

    public string ActionIcon => Action switch
    {
        SyncActionType.CopyLeftToRight => "➡️",
        SyncActionType.CopyRightToLeft => "⬅️",
        SyncActionType.DeleteRight => "❌ Right",
        SyncActionType.DeleteLeft => "❌ Left",
        SyncActionType.Conflict => "⚠️ Conflict",
        SyncActionType.InSync => "✔️ In Sync",
        SyncActionType.Skipped => "⏭️ Skipped",
        SyncActionType.Error => "🚫 Error",
        _ => "?"
    };
}

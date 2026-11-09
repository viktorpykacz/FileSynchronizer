namespace FileSynchronizer.Core.Models;

/// <summary>
/// Result of the analysis step containing planned actions for all files.
/// </summary>
public class SyncPlan
{
    public Guid JobId { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public required string ResolvedLeftPath { get; init; }
    public required string ResolvedRightPath { get; init; }
    public List<SyncItem> Items { get; init; } = new();

    public int TotalItemsCount => Items.Count;
    public int ActionRequiredCount => Items.Count(i => i.RequiresAction);
    public int InSyncCount => Items.Count(i => i.Action == SyncActionType.InSync);
    public int ConflictCount => Items.Count(i => i.Action == SyncActionType.Conflict);
    public int LeftToRightCount => Items.Count(i => i.Action == SyncActionType.CopyLeftToRight);
    public int RightToLeftCount => Items.Count(i => i.Action == SyncActionType.CopyRightToLeft);
    public int DeleteCount => Items.Count(i => i.Action is SyncActionType.DeleteLeft or SyncActionType.DeleteRight);
    public int SkippedCount => Items.Count(i => i.Action == SyncActionType.Skipped);

    public long TotalBytesToTransfer => Items
        .Where(i => i.Action is SyncActionType.CopyLeftToRight or SyncActionType.CopyRightToLeft)
        .Sum(i => i.TotalBytes);

    public bool HasConflicts => ConflictCount > 0;
}

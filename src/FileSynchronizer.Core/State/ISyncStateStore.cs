namespace FileSynchronizer.Core.State;

public interface ISyncStateStore
{
    Task<SyncStateSnapshot?> LoadSnapshotAsync(Guid jobId, CancellationToken ct = default);
    Task SaveSnapshotAsync(SyncStateSnapshot snapshot, CancellationToken ct = default);
    Task DeleteSnapshotAsync(Guid jobId, CancellationToken ct = default);
}

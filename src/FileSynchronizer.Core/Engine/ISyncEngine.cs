using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Engine;

public interface ISyncEngine
{
    /// <summary>
    /// Analyzes the job folders and returns a planned list of sync actions.
    /// </summary>
    Task<SyncPlan> AnalyzeAsync(
        SyncJob job,
        IProgress<SyncProgressReport>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes the planned synchronization actions.
    /// </summary>
    Task<SyncResult> ExecuteAsync(
        SyncJob job,
        SyncPlan plan,
        IProgress<SyncProgressReport>? progress = null,
        CancellationToken ct = default);
}

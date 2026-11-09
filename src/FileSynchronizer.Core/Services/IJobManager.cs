using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Services;

public interface IJobManager
{
    IReadOnlyList<SyncJob> GetAllJobs();
    SyncJob? GetJobById(Guid id);
    Task AddJobAsync(SyncJob job, CancellationToken ct = default);
    Task UpdateJobAsync(SyncJob job, CancellationToken ct = default);
    Task DeleteJobAsync(Guid id, CancellationToken ct = default);
    Task LoadJobsAsync(CancellationToken ct = default);
    Task SaveJobsAsync(CancellationToken ct = default);
}

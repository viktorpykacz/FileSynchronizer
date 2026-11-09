using System.IO;
using System.Text.Json;
using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Services;

/// <summary>
/// Manages persistence and lifecycle of synchronization jobs.
/// </summary>
public class JobManager : IJobManager
{
    private readonly string _configFilePath;
    private readonly List<SyncJob> _jobs = new();
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public JobManager(string? configFilePath = null)
    {
        if (configFilePath != null)
        {
            _configFilePath = configFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "FileSynchronizer");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _configFilePath = Path.Combine(dir, "jobs.json");
        }
    }

    public IReadOnlyList<SyncJob> GetAllJobs()
    {
        lock (_lock)
        {
            return _jobs.ToList();
        }
    }

    public SyncJob? GetJobById(Guid id)
    {
        lock (_lock)
        {
            return _jobs.FirstOrDefault(j => j.Id == id);
        }
    }

    public async Task AddJobAsync(SyncJob job, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _jobs.Add(job);
        }
        await SaveJobsAsync(ct);
    }

    public async Task UpdateJobAsync(SyncJob job, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var idx = _jobs.FindIndex(j => j.Id == job.Id);
            if (idx >= 0)
            {
                _jobs[idx] = job;
            }
            else
            {
                _jobs.Add(job);
            }
        }
        await SaveJobsAsync(ct);
    }

    public async Task DeleteJobAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _jobs.RemoveAll(j => j.Id == id);
        }
        await SaveJobsAsync(ct);
    }

    public async Task LoadJobsAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_configFilePath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_configFilePath);
            var loaded = await JsonSerializer.DeserializeAsync<List<SyncJob>>(stream, JsonOptions, ct);
            if (loaded != null)
            {
                lock (_lock)
                {
                    _jobs.Clear();
                    _jobs.AddRange(loaded);
                }
            }
        }
        catch
        {
            // Ignore load failures or corrupt files
        }
    }

    public async Task SaveJobsAsync(CancellationToken ct = default)
    {
        var tempFile = _configFilePath + ".tmp";
        var dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        List<SyncJob> snapshot;
        lock (_lock)
        {
            snapshot = _jobs.ToList();
        }

        await using (var stream = File.Create(tempFile))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, ct);
        }

        if (File.Exists(_configFilePath))
        {
            File.Delete(_configFilePath);
        }
        File.Move(tempFile, _configFilePath);
    }
}

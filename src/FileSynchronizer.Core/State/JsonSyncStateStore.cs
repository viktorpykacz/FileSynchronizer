using System.IO;
using System.Text.Json;

namespace FileSynchronizer.Core.State;

/// <summary>
/// JSON file-based implementation of sync state snapshot store.
/// Stores snapshots in %AppData%/FileSynchronizer/snapshots or custom folder.
/// </summary>
public class JsonSyncStateStore : ISyncStateStore
{
    private readonly string _storageDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public JsonSyncStateStore(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileSynchronizer",
            "snapshots");

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    private string GetSnapshotFilePath(Guid jobId) => Path.Combine(_storageDirectory, $"{jobId}.json");

    public async Task<SyncStateSnapshot?> LoadSnapshotAsync(Guid jobId, CancellationToken ct = default)
    {
        var path = GetSnapshotFilePath(jobId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<SyncStateSnapshot>(stream, JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveSnapshotAsync(SyncStateSnapshot snapshot, CancellationToken ct = default)
    {
        var path = GetSnapshotFilePath(snapshot.JobId);
        var tempPath = path + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, ct);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(tempPath, path);
    }

    public Task DeleteSnapshotAsync(Guid jobId, CancellationToken ct = default)
    {
        var path = GetSnapshotFilePath(jobId);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch { }
        }
        return Task.CompletedTask;
    }
}

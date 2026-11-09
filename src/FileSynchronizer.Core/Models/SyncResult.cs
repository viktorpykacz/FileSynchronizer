namespace FileSynchronizer.Core.Models;

/// <summary>
/// Progress notification emitted during analysis or execution.
/// </summary>
public record SyncProgressReport
{
    public string Phase { get; init; } = string.Empty; // "Analiza...", "Kopiowanie...", "Usuwanie..."
    public string? CurrentItemPath { get; init; }
    public int ProcessedItems { get; init; }
    public int TotalItems { get; init; }
    public long BytesTransferred { get; init; }
    public long TotalBytes { get; init; }
    public double BytesPerSecond { get; init; }
    
    public double ItemPercentage => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100.0 : 0.0;
    public double BytesPercentage => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100.0 : 0.0;
}

/// <summary>
/// Execution summary after a sync run completes.
/// </summary>
public class SyncResult
{
    public bool Success { get; init; }
    public TimeSpan Duration { get; init; }
    public int FilesCopied { get; init; }
    public int FilesDeleted { get; init; }
    public int FilesSkipped { get; init; }
    public int ErrorsCount { get; init; }
    public long TotalBytesTransferred { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<SyncItem> ProcessedItems { get; init; } = new();
    public string SummaryMessage { get; init; } = string.Empty;
}

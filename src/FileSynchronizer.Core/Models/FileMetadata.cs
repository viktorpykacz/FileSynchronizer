namespace FileSynchronizer.Core.Models;

/// <summary>
/// Metadata describing a file on either the Left or Right side.
/// </summary>
public record FileMetadata
{
    public required string RelativePath { get; init; }
    public required string FullPath { get; init; }
    public long Size { get; init; }
    public DateTime LastModifiedUtc { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string? Hash { get; set; }
    public bool Exists { get; init; } = true;
    public bool IsDirectory { get; init; } = false;
    public FileAttributes Attributes { get; init; }

    public static FileMetadata FromFileInfo(FileInfo fi, string relativePath)
    {
        return new FileMetadata
        {
            RelativePath = relativePath.Replace('\\', '/'),
            FullPath = fi.FullName,
            Size = fi.Length,
            LastModifiedUtc = fi.LastWriteTimeUtc,
            CreatedUtc = fi.CreationTimeUtc,
            Attributes = fi.Attributes,
            Exists = true,
            IsDirectory = false
        };
    }
}

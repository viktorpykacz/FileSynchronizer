using System.IO;
using System.IO.Hashing;

namespace FileSynchronizer.Core.Engine;

/// <summary>
/// High performance file hashing utility.
/// </summary>
public static class FileHasher
{
    private const int BufferSize = 1024 * 512; // 512 KB buffer

    /// <summary>
    /// Computes XxHash64 hash string for a file (fastest modern non-cryptographic checksum).
    /// </summary>
    public static async Task<string> ComputeXxHash64Async(string filePath, CancellationToken ct = default)
    {
        var hasher = new XxHash64();
        byte[] buffer = new byte[BufferSize];

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            hasher.Append(buffer.AsSpan(0, bytesRead));
        }

        var hashBytes = hasher.GetCurrentHash();
        return Convert.ToHexString(hashBytes);
    }
}

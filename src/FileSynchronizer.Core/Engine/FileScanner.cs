using System.IO;
using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Engine;

/// <summary>
/// Scans local directories recursively, building file metadata with exclusion/inclusion filtering.
/// </summary>
public static class FileScanner
{
    public static async Task<Dictionary<string, FileMetadata>> ScanDirectoryAsync(
        string rootPath,
        IReadOnlyList<string> excludePatterns,
        IReadOnlyList<string> includePatterns,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(rootPath))
        {
            return result;
        }

        var rootDir = new DirectoryInfo(rootPath);
        var queue = new Queue<DirectoryInfo>();
        queue.Enqueue(rootDir);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var currentDir = queue.Dequeue();

            // Check if directory itself is excluded
            var dirRelPath = GetRelativePath(rootDir.FullName, currentDir.FullName);
            if (!string.IsNullOrEmpty(dirRelPath) && PatternMatcher.MatchesAny(dirRelPath + "/", excludePatterns))
            {
                continue;
            }

            progress?.Report($"Skanowanie: {currentDir.FullName}");

            // Enumerate subdirectories safely
            try
            {
                foreach (var subDir in currentDir.EnumerateDirectories())
                {
                    // Skip hidden system reparse points
                    if ((subDir.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        continue;
                    }

                    var subRel = GetRelativePath(rootDir.FullName, subDir.FullName);
                    if (!PatternMatcher.MatchesAny(subRel + "/", excludePatterns))
                    {
                        queue.Enqueue(subDir);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories without permission
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            // Enumerate files safely
            try
            {
                foreach (var file in currentDir.EnumerateFiles())
                {
                    ct.ThrowIfCancellationRequested();

                    var fileRel = GetRelativePath(rootDir.FullName, file.FullName);

                    // Check exclusions
                    if (PatternMatcher.MatchesAny(fileRel, excludePatterns))
                    {
                        continue;
                    }

                    // Check inclusions if configured
                    if (includePatterns.Count > 0 && !PatternMatcher.MatchesAny(fileRel, includePatterns))
                    {
                        continue;
                    }

                    var metadata = FileMetadata.FromFileInfo(file, fileRel);
                    result[metadata.RelativePath] = metadata;
                }
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (FileNotFoundException)
            {
                continue;
            }
        }

        return result;
    }

    private static string GetRelativePath(string rootPath, string fullPath)
    {
        if (string.Equals(rootPath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var rel = Path.GetRelativePath(rootPath, fullPath);
        return rel.Replace('\\', '/');
    }
}

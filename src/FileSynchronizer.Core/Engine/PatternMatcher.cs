using System.Text.RegularExpressions;

namespace FileSynchronizer.Core.Engine;

/// <summary>
/// Handles matching file paths against glob patterns (*.tmp, Thumbs.db, .git/*, etc.).
/// </summary>
public static class PatternMatcher
{
    private static readonly Dictionary<string, Regex> RegexCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();

    /// <summary>
    /// Checks if a relative path matches any pattern in the list.
    /// </summary>
    public static bool MatchesAny(string relativePath, IEnumerable<string> patterns)
    {
        var normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        var fileName = Path.GetFileName(normalizedPath);

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            var cleanPattern = pattern.Trim().Replace('\\', '/');

            // Exact file name match or extension match
            if (cleanPattern.StartsWith("*.") && !cleanPattern.Contains('/'))
            {
                var ext = cleanPattern.Substring(1);
                if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var regex = GetOrCreateRegex(cleanPattern);
            if (regex.IsMatch(normalizedPath) || regex.IsMatch(fileName))
            {
                return true;
            }
        }

        return false;
    }

    private static Regex GetOrCreateRegex(string pattern)
    {
        lock (CacheLock)
        {
            if (RegexCache.TryGetValue(pattern, out var cached))
            {
                return cached;
            }

            // Convert glob pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", ".") + "$";

            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            RegexCache[pattern] = regex;
            return regex;
        }
    }
}

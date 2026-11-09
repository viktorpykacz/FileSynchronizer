using FileSynchronizer.Core.Engine;
using Xunit;

namespace FileSynchronizer.Core.Tests;

public class PatternMatcherTests
{
    [Theory]
    [InlineData("temp.tmp", "*.tmp", true)]
    [InlineData("sub/folder/file.tmp", "*.tmp", true)]
    [InlineData("document.docx", "*.tmp", false)]
    [InlineData("~$document.docx", "~$*", true)]
    [InlineData("Thumbs.db", "Thumbs.db", true)]
    [InlineData(".git/config", ".git/*", true)]
    [InlineData("src/app.cs", ".git/*", false)]
    public void MatchesAny_ValidatesGlobPatterns(string relativePath, string pattern, bool expected)
    {
        var result = PatternMatcher.MatchesAny(relativePath, new[] { pattern });
        Assert.Equal(expected, result);
    }
}

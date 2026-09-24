using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class LongPathTests
{
    [Fact]
    public void ShortPathsAreLeftAlone()
    {
        Assert.Equal(@"C:\temp\a.txt", LongPath.ForIo(@"C:\temp\a.txt"));
    }

    [Fact]
    public void LongPathGetsTheExtendedPrefixOnWindows()
    {
        string deep = @"C:\" + string.Join(@"\", Enumerable.Repeat(new string('d', 40), 9));
        string io = LongPath.ForIo(deep);

        if (OperatingSystem.IsWindows())
            Assert.Equal(@"\\?\" + deep, io);
        else
            Assert.Equal(deep, io);
    }

    [Fact]
    public void DisplayStripsThePrefixBackOff()
    {
        Assert.Equal(@"C:\a\b", LongPath.Display(@"\\?\C:\a\b"));
        Assert.Equal(@"\\server\share\a", LongPath.Display(@"\\?\UNC\server\share\a"));
        Assert.Equal(@"C:\a\b", LongPath.Display(@"C:\a\b"));
    }

    [Fact]
    public void AlreadyExtendedPathsAreNotPrefixedTwice()
    {
        Assert.Equal(@"\\?\C:\a", LongPath.ForIo(@"\\?\C:\a"));
    }

    [Fact]
    public void PathsAreComparedByTheirPlainForm()
    {
        Assert.True(PathCompare.PathMatches(@"\\?\C:\a\b", @"C:\a\b"));
    }
}

using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PathCompletionTests
{
    private static readonly char Sep = System.IO.Path.DirectorySeparatorChar;

    /// <summary>A path written the way this machine writes one.</summary>
    private static string P(params string[] parts) => System.IO.Path.Combine(parts);

    private static string Root => PathCompare.OSRootPath();

    // ── splitting ────────────────────────────────────────────────────────

    [Fact]
    public void TextWithNoSeparatorNamesTheFolderBeingLookedAt()
    {
        PathFragment fragment = PathCompletion.Split("Doc", Root);

        Assert.Equal(Root, fragment.Directory);
        Assert.Equal("Doc", fragment.Prefix);
    }

    [Fact]
    public void EmptyTextOffersTheWholeFolderBeingLookedAt()
    {
        PathFragment fragment = PathCompletion.Split(string.Empty, Root);

        Assert.Equal(Root, fragment.Directory);
        Assert.Equal(string.Empty, fragment.Prefix);
    }

    [Fact]
    public void TextEndingInASeparatorIsAllFolderAndNoName()
    {
        using TempDir temp = new();
        string typed = temp.Path + Sep;

        PathFragment fragment = PathCompletion.Split(typed, Root);

        Assert.Equal(temp.Path, fragment.Directory);
        Assert.Equal(string.Empty, fragment.Prefix);
    }

    [Fact]
    public void TheLastSeparatorIsWhereTheNameStarts()
    {
        using TempDir temp = new();
        temp.Dir("inner");

        PathFragment fragment = PathCompletion.Split(P(temp.Path, "inn"), Root);

        Assert.Equal(temp.Path, fragment.Directory);
        Assert.Equal("inn", fragment.Prefix);
    }

    [Fact]
    public void ARelativeNameIsMeasuredFromTheFolderBeingLookedAt()
    {
        using TempDir temp = new();
        temp.Dir("inner");

        PathFragment fragment = PathCompletion.Split("inner" + Sep + "th", temp.Path);

        Assert.Equal(P(temp.Path, "inner"), fragment.Directory);
        Assert.Equal("th", fragment.Prefix);
    }

    [Fact]
    public void AFolderThatIsNotThereIsStillSplitOut()
    {
        using TempDir temp = new();

        PathFragment fragment = PathCompletion.Split(P(temp.Path, "nowhere", "a"), Root);

        Assert.Equal(P(temp.Path, "nowhere"), fragment.Directory);
        Assert.Equal("a", fragment.Prefix);
    }

    // ── putting a name back in ───────────────────────────────────────────

    [Fact]
    public void ChoosingAFileReplacesTheHalfTypedName()
    {
        Assert.Equal(
            P("var", "log") + Sep + "syslog",
            PathCompletion.Join(P("var", "log") + Sep + "sys", "syslog", isDirectory: false));
    }

    [Fact]
    public void ChoosingAFolderLeavesASeparatorToCarryOnFrom()
    {
        Assert.Equal(
            P("var", "log") + Sep,
            PathCompletion.Join(P("var") + Sep + "lo", "log", isDirectory: true));
    }

    [Fact]
    public void ANameWithNoFolderInFrontOfItStandsAlone()
    {
        Assert.Equal("Documents" + Sep, PathCompletion.Join("Doc", "Documents", isDirectory: true));
    }

    [Fact]
    public void TheSeparatorAlreadyBeingUsedIsTheOneWrittenBack()
    {
        Assert.Equal("/var/log/", PathCompletion.Join("/var/lo", "log", isDirectory: true));
    }

    // ── how far the names agree ──────────────────────────────────────────

    [Fact]
    public void TabCarriesTheTextAsFarAsEveryMatchAgrees()
    {
        Assert.Equal(
            "report",
            PathCompletion.LongestCommonPrefix(["report-jan.txt", "report-feb.txt", "reports"]));
    }

    [Fact]
    public void NamesThatShareNothingCarryNothing()
    {
        Assert.Equal(string.Empty, PathCompletion.LongestCommonPrefix(["alpha", "beta"]));
    }

    [Fact]
    public void OneNameAgreesWithItselfAllTheWay()
    {
        Assert.Equal("only.txt", PathCompletion.LongestCommonPrefix(["only.txt"]));
    }

    [Fact]
    public void NoNamesCarryNothing()
    {
        Assert.Equal(string.Empty, PathCompletion.LongestCommonPrefix([]));
    }

    [Fact]
    public void AShorterNameStopsTheAgreementThere()
    {
        Assert.Equal("doc", PathCompletion.LongestCommonPrefix(["doc", "documents"]));
    }

    // ── matching ─────────────────────────────────────────────────────────

    [Fact]
    public void NothingTypedMatchesEverything()
    {
        Assert.True(PathCompletion.Matches("anything", string.Empty));
    }

    [Fact]
    public void AMatchIsAStartNotAContains()
    {
        Assert.True(PathCompletion.Matches("report.txt", "rep"));
        Assert.False(PathCompletion.Matches("my-report.txt", "rep"));
    }

    [Fact]
    public void CapitalsCountWhereTheFilesystemSaysTheyDo()
    {
        bool expected = !OperatingSystem.IsWindows();
        Assert.Equal(!expected, PathCompletion.Matches("Report.txt", "rep"));
    }
}

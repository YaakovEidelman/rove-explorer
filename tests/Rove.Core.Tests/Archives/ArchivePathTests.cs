using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class ArchivePathTests
{
    [Fact]
    public void APathThroughNoZipIsNotOne()
    {
        using TempDir tmp = new();
        tmp.Dir("plain");

        Assert.False(ArchivePath.TryParse(tmp.Sub("plain"), out _));
        Assert.False(ArchivePath.IsInside(tmp.Path));
    }

    [Fact]
    public void TheZipItselfIsTheTopOfTheArchive()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        Assert.True(ArchivePath.TryParse(tmp.Sub("pack.zip"), out ArchivePath at));
        Assert.Equal(tmp.Sub("pack.zip"), at.Archive);
        Assert.Equal(string.Empty, at.Entry);
        Assert.True(at.IsRoot);
        Assert.Equal("pack.zip", at.Name);
    }

    [Fact]
    public void TheWayThroughIsKeptTheWayAZipWritesIt()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        Assert.True(ArchivePath.TryParse(tmp.Sub("pack.zip", "sub", "b.txt"), out ArchivePath at));
        Assert.Equal("sub/b.txt", at.Entry);
        Assert.Equal("b.txt", at.Name);
        Assert.Equal(tmp.Sub("pack.zip", "sub", "b.txt"), at.FullPath);
    }

    [Fact]
    public void AZipThatIsNotReallyThereIsJustAName()
    {
        using TempDir tmp = new();

        Assert.False(ArchivePath.TryParse(tmp.Sub("missing.zip", "sub"), out _));
    }

    [Fact]
    public void AFolderNamedLikeAZipIsStillAFolder()
    {
        using TempDir tmp = new();
        tmp.Dir("pack.zip");

        Assert.False(ArchivePath.TryParse(tmp.Sub("pack.zip", "inside"), out _));
    }

    [Fact]
    public void AnExtensionThatOnlyStartsWithZipIsNotOne()
    {
        using TempDir tmp = new();
        tmp.File("pack.zipper", "not an archive");

        Assert.False(ArchivePath.TryParse(tmp.Sub("pack.zipper"), out _));
    }

    [Fact]
    public void TheFirstZipOnTheWayInIsTheOneThatCounts()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("outer.zip"), ("inner.zip", "pretend"));

        Assert.True(ArchivePath.TryParse(tmp.Sub("outer.zip", "inner.zip", "a.txt"), out ArchivePath at));
        Assert.Equal(tmp.Sub("outer.zip"), at.Archive);
        Assert.Equal("inner.zip/a.txt", at.Entry);
    }

    [Fact]
    public void UpWalksOutOneStepAtATimeAndThenStops()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/deep/b.txt", "two"));
        ArchivePath.TryParse(tmp.Sub("pack.zip", "sub", "deep"), out ArchivePath at);

        ArchivePath up = at.Parent!.Value;
        Assert.Equal("sub", up.Entry);

        ArchivePath top = up.Parent!.Value;
        Assert.True(top.IsRoot);
        Assert.Null(top.Parent);
    }

    [Fact]
    public void GoingDownNamesTheChildTheWayTheZipWouldHaveIt()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));
        ArchivePath.TryParse(tmp.Sub("pack.zip"), out ArchivePath at);

        Assert.Equal("sub", at.Down("sub").Entry);
        Assert.Equal("sub/b.txt", at.Down("sub").Down("b.txt").Entry);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    [InlineData("./sneak.txt")]
    [InlineData("")]
    public void AnEntryNameThatClimbsOutIsNotSafe(string name) =>
        Assert.False(ArchivePath.IsSafeEntryName(name));

    [Theory]
    [InlineData("a.txt")]
    [InlineData("sub/b.txt")]
    [InlineData("sub/deep/")]
    public void AnOrdinaryEntryNameIsSafe(string name) =>
        Assert.True(ArchivePath.IsSafeEntryName(name));
}

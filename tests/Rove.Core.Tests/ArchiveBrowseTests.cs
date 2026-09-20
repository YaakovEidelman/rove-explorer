using System.IO.Compression;
using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public static class ZipBuilder
{
    public static string Make(string path, params (string Name, string Content)[] entries)
    {
        using FileStream stream = new(LongPath.ForIo(path), FileMode.Create);
        using ZipArchive zip = new(stream, ZipArchiveMode.Create);
        foreach ((string name, string content) in entries)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);
            if (name.EndsWith('/'))
                continue;
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }
        return path;
    }
}

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

public class ArchiveBrowseTests
{
    private readonly Actions _actions = new();

    private FolderItem[] Read(string path)
    {
        CommandResult<FolderItem[]> result = _actions.ReadDirectory(new(path));
        Assert.True(result.IsOk, result.Message);
        return result.Data!;
    }

    [Fact]
    public void AZipListsLikeAFolder()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"), ("sub/b.txt", "two"));

        FolderItem[] items = Read(tmp.Sub("pack.zip"));

        Assert.Equal(["a.txt", "sub"], items.Select(i => i.Name).OrderBy(n => n));
        Assert.True(items.Single(i => i.Name == "sub").IsDirectory);
        Assert.False(items.Single(i => i.Name == "a.txt").IsDirectory);
    }

    [Fact]
    public void AFolderShowsEvenThoughTheZipNeverWroteItDown()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        FolderItem[] items = Read(tmp.Sub("pack.zip"));

        FolderItem only = Assert.Single(items);
        Assert.Equal("sub", only.Name);
        Assert.True(only.IsDirectory);
    }

    [Fact]
    public void AFolderInsideListsItsOwnThings()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"),
            ("a.txt", "one"), ("sub/b.txt", "two"), ("sub/deep/c.txt", "three"));

        FolderItem[] items = Read(tmp.Sub("pack.zip", "sub"));

        Assert.Equal(["b.txt", "deep"], items.Select(i => i.Name).OrderBy(n => n));
    }

    [Fact]
    public void NothingFromDeeperInLeaksIntoTheTop()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/deep/c.txt", "three"));

        FolderItem[] items = Read(tmp.Sub("pack.zip"));

        Assert.Equal("sub", Assert.Single(items).Name);
    }

    [Fact]
    public void AFolderWrittenDownOnlyShowsOnce()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/", ""), ("sub/b.txt", "two"));

        FolderItem[] items = Read(tmp.Sub("pack.zip"));

        Assert.Equal("sub", Assert.Single(items).Name);
    }

    [Fact]
    public void AnEmptyFolderInsideTheZipIsStillAFolder()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("empty/", ""), ("a.txt", "one"));

        FolderItem[] items = Read(tmp.Sub("pack.zip"));

        Assert.True(items.Single(i => i.Name == "empty").IsDirectory);
        Assert.Empty(Read(tmp.Sub("pack.zip", "empty")));
    }

    [Fact]
    public void AnEntrySaysHowBigItIsAndWhatKindItIs()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("notes.txt", "hello"));

        FolderItem item = Assert.Single(Read(tmp.Sub("pack.zip")));

        Assert.Equal(5, item.Size);
        Assert.Equal(".txt", item.Extension);
        Assert.Equal(tmp.Sub("pack.zip", "notes.txt"), item.FullPath);
    }

    [Fact]
    public void AFolderInsideHasNoSizeOfItsOwn()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        Assert.Null(Assert.Single(Read(tmp.Sub("pack.zip"))).Size);
    }

    [Fact]
    public void AnEntryThatClimbsOutOfTheArchiveIsLeftOut()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("../escape.txt", "bad"), ("a.txt", "one"));

        FolderItem[] items = Read(tmp.Sub("pack.zip"));

        Assert.Equal("a.txt", Assert.Single(items).Name);
    }

    [Fact]
    public void ADamagedZipSaysSoInsteadOfThrowing()
    {
        using TempDir tmp = new();
        tmp.File("pack.zip", "this is not a zip at all");

        CommandResult<FolderItem[]> result = _actions.ReadDirectory(new(tmp.Sub("pack.zip")));

        Assert.False(result.IsOk);
        Assert.Equal("bad_archive", result.Reason);
    }

    [Fact]
    public void AFolderThatIsNotInTheZipListsAsNothing()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        Assert.Empty(Read(tmp.Sub("pack.zip", "nowhere")));
    }

    [Fact]
    public void AZipWithALongPathToItStillReads()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();
        ZipBuilder.Make(Path.Combine(deep, "pack.zip"), ("a.txt", "one"));

        Assert.Equal("a.txt", Assert.Single(Read(Path.Combine(deep, "pack.zip"))).Name);
    }
}

public class ArchiveParentTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void UpFromInsideStaysInsideTheZip()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/deep/c.txt", "three"));

        CommandResult<FolderItem?> up = _actions.GetParent(new(tmp.Sub("pack.zip", "sub", "deep")));

        Assert.True(up.IsOk);
        Assert.Equal(tmp.Sub("pack.zip", "sub"), up.Data!.FullPath);
        Assert.True(up.Data.IsDirectory);
    }

    [Fact]
    public void UpFromTheTopOfTheZipIsTheFolderItSitsIn()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<FolderItem?> up = _actions.GetParent(new(tmp.Sub("pack.zip")));

        Assert.True(up.IsOk);
        Assert.Equal(tmp.Path, up.Data!.FullPath);
    }

    [Fact]
    public void UpFromJustInsideTheTopIsTheTop()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        CommandResult<FolderItem?> up = _actions.GetParent(new(tmp.Sub("pack.zip", "sub")));

        Assert.Equal(tmp.Sub("pack.zip"), up.Data!.FullPath);
    }
}

public class ArchiveResolvePathTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void ATypedPathToAFolderInsideTheZipGoesThere()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        CommandResult<FolderItem?> found = _actions.ResolvePath(new(tmp.Sub("pack.zip", "sub"), tmp.Path));

        Assert.True(found.IsOk, found.Message);
        Assert.True(found.Data!.IsDirectory);
        Assert.Equal(tmp.Sub("pack.zip", "sub"), found.Data.FullPath);
    }

    [Fact]
    public void ATypedPathToAFileInsideTheZipFindsTheFile()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        CommandResult<FolderItem?> found = _actions.ResolvePath(new(tmp.Sub("pack.zip", "sub", "b.txt"), tmp.Path));

        Assert.True(found.IsOk, found.Message);
        Assert.False(found.Data!.IsDirectory);
        Assert.Equal("b.txt", found.Data.Name);
    }

    [Fact]
    public void ATypedPathToNothingInsideTheZipIsNotFound()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<FolderItem?> found = _actions.ResolvePath(new(tmp.Sub("pack.zip", "nope.txt"), tmp.Path));

        Assert.False(found.IsOk);
        Assert.Equal("not_found", found.Reason);
    }

    [Fact]
    public void ATypedPathToTheZipItselfStillFindsTheFileOnDisk()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<FolderItem?> found = _actions.ResolvePath(new(tmp.Sub("pack.zip"), tmp.Path));

        Assert.True(found.IsOk);
        Assert.False(found.Data!.IsDirectory);
    }
}

public class ArchiveCopyOutTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void AFileComesOutWhereSomethingElseCanOpenIt()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        CommandResult<string?> copy = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "sub", "b.txt")));

        Assert.True(copy.IsOk, copy.Message);
        Assert.Equal("two", File.ReadAllText(copy.Data!));
        Assert.Equal("b.txt", Path.GetFileName(copy.Data!));
    }

    [Fact]
    public void TheSameEntryComesBackToTheSamePlace()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        string first = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "a.txt"))).Data!;
        string again = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "a.txt"))).Data!;

        Assert.Equal(first, again);
    }

    [Fact]
    public void TwoZipsOfTheSameNameDoNotShareACopy()
    {
        using TempDir tmp = new();
        tmp.Dir("left");
        tmp.Dir("right");
        ZipBuilder.Make(tmp.Sub("left", "pack.zip"), ("a.txt", "left"));
        ZipBuilder.Make(tmp.Sub("right", "pack.zip"), ("a.txt", "right"));

        string left = _actions.CopyOutOfArchive(new(tmp.Sub("left", "pack.zip", "a.txt"))).Data!;
        string right = _actions.CopyOutOfArchive(new(tmp.Sub("right", "pack.zip", "a.txt"))).Data!;

        Assert.NotEqual(left, right);
        Assert.Equal("left", File.ReadAllText(left));
        Assert.Equal("right", File.ReadAllText(right));
    }

    [Fact]
    public void ANewerZipReplacesTheCopyTakenFromTheOldOne()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "before"));
        string copy = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "a.txt"))).Data!;
        Assert.Equal("before", File.ReadAllText(copy));

        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "after"));
        File.SetLastWriteTimeUtc(tmp.Sub("pack.zip"), DateTime.UtcNow.AddMinutes(1));

        string again = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "a.txt"))).Data!;

        Assert.Equal(copy, again);
        Assert.Equal("after", File.ReadAllText(again));
    }

    [Fact]
    public void AskingForAnEntryThatIsGoneSaysSo()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<string?> copy = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "nope.txt")));

        Assert.False(copy.IsOk);
        Assert.Equal("not_found", copy.Reason);
    }

    [Fact]
    public void AskingForSomethingThatIsNotInAZipIsRefused()
    {
        using TempDir tmp = new();
        tmp.File("loose.txt", "one");

        CommandResult<string?> copy = _actions.CopyOutOfArchive(new(tmp.Sub("loose.txt")));

        Assert.False(copy.IsOk);
        Assert.Equal("not_in_archive", copy.Reason);
    }

    [Fact]
    public void NothingCanBeLaunchedWhileItIsStillInsideAZip()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<string?> launched = _actions.LaunchFile(new(tmp.Sub("pack.zip", "a.txt")));

        Assert.False(launched.IsOk);
        Assert.Equal("in_archive", launched.Reason);
    }
}

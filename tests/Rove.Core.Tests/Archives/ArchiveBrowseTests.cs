using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

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

    [Fact]
    public void ATarGzListsLikeAFolder()
    {
        using TempDir tmp = new();
        TarBuilder.Make(tmp.Sub("pack.tar.gz"), gzip: true, ("a.txt", "one"), ("sub/b.txt", "two"));

        FolderItem[] items = Read(tmp.Sub("pack.tar.gz"));

        Assert.Equal(["a.txt", "sub"], items.Select(i => i.Name).OrderBy(n => n));
        Assert.True(items.Single(i => i.Name == "sub").IsDirectory);
    }

    [Fact]
    public void ATarGzFolderInsideListsItsOwnThings()
    {
        using TempDir tmp = new();
        TarBuilder.Make(tmp.Sub("pack.tar.gz"), gzip: true,
            ("a.txt", "one"), ("sub/b.txt", "two"), ("sub/deep/c.txt", "three"));

        FolderItem[] items = Read(tmp.Sub("pack.tar.gz", "sub"));

        Assert.Equal(["b.txt", "deep"], items.Select(i => i.Name).OrderBy(n => n));
    }

    [Fact]
    public void APlainTarListsLikeAFolder()
    {
        using TempDir tmp = new();
        TarBuilder.Make(tmp.Sub("pack.tar"), gzip: false, ("notes.txt", "hello"));

        FolderItem item = Assert.Single(Read(tmp.Sub("pack.tar")));

        Assert.Equal("notes.txt", item.Name);
        Assert.Equal(5, item.Size);
    }

    [Fact]
    public void ADamagedTarGzSaysSoInsteadOfThrowing()
    {
        using TempDir tmp = new();
        tmp.File("pack.tar.gz", "this is not a tar.gz at all");

        CommandResult<FolderItem[]> result = _actions.ReadDirectory(new(tmp.Sub("pack.tar.gz")));

        Assert.False(result.IsOk);
        Assert.Equal("bad_archive", result.Reason);
    }
}

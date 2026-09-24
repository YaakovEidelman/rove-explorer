using System.IO.Compression;
using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class ExtractArchiveTests
{
    private readonly Actions _actions = new();

    private static string Zip(string path, params (string Name, string Content)[] entries)
    {
        using FileStream stream = new(path, FileMode.Create);
        using ZipArchive zip = new(stream, ZipArchiveMode.Create);
        foreach ((string name, string content) in entries)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }
        return path;
    }

    [Fact]
    public void UnpacksIntoAFolderNamedAfterTheArchive()
    {
        using TempDir tmp = new();
        Zip(tmp.Sub("pack.zip"), ("a.txt", "one"), ("sub/b.txt", "two"));

        CommandResult<OpResult[]> result = _actions.ExtractArchives(new([tmp.Sub("pack.zip")], tmp.Path));

        Assert.True(result.IsOk);
        Assert.Equal("one", File.ReadAllText(tmp.Sub("pack", "a.txt")));
        Assert.Equal("two", File.ReadAllText(tmp.Sub("pack", "sub", "b.txt")));
        Assert.Equal(tmp.Sub("pack"), result.Data![0].Item!.FullPath);
    }

    [Fact]
    public void SecondExtractTakesTheNextFreeName()
    {
        using TempDir tmp = new();
        Zip(tmp.Sub("pack.zip"), ("a.txt", "one"));

        _actions.ExtractArchives(new([tmp.Sub("pack.zip")], tmp.Path));
        CommandResult<OpResult[]> again = _actions.ExtractArchives(new([tmp.Sub("pack.zip")], tmp.Path));

        Assert.True(again.IsOk);
        Assert.True(File.Exists(tmp.Sub("pack (2)", "a.txt")));
    }

    [Fact]
    public void EmptyFoldersInsideTheArchiveComeOutToo()
    {
        using TempDir tmp = new();
        Zip(tmp.Sub("pack.zip"), ("empty/", ""), ("a.txt", "one"));

        _actions.ExtractArchives(new([tmp.Sub("pack.zip")], tmp.Path));

        Assert.True(Directory.Exists(tmp.Sub("pack", "empty")));
    }

    [Fact]
    public void AnEntryClimbingOutOfTheFolderIsRefused()
    {
        using TempDir tmp = new();
        string inside = tmp.Dir("here");
        Zip(Path.Combine(inside, "pack.zip"), ("../escaped.txt", "gotcha"));

        CommandResult<OpResult[]> result = _actions.ExtractArchives(new([Path.Combine(inside, "pack.zip")], inside));

        Assert.False(result.IsOk);
        Assert.False(File.Exists(tmp.Sub("here", "escaped.txt")));
        Assert.False(Directory.Exists(tmp.Sub("here", "pack")));
    }

    [Fact]
    public void SomethingThatIsNotAZipIsNotExtracted()
    {
        using TempDir tmp = new();
        tmp.File("notes.txt", "hello");

        CommandResult<OpResult[]> result = _actions.ExtractArchives(new([tmp.Sub("notes.txt")], tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("not_an_archive", result.Reason);
    }

    [Fact]
    public void ADamagedZipLeavesNothingBehind()
    {
        using TempDir tmp = new();
        tmp.File("pack.zip", "this is not really a zip");

        CommandResult<OpResult[]> result = _actions.ExtractArchives(new([tmp.Sub("pack.zip")], tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("bad_archive", result.Reason);
        Assert.False(Directory.Exists(tmp.Sub("pack")));
    }

    [Fact]
    public void MissingArchiveFailsWithNotFound()
    {
        using TempDir tmp = new();

        CommandResult<OpResult[]> result = _actions.ExtractArchives(new([tmp.Sub("gone.zip")], tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }

    [Fact]
    public async Task CancelledExtractCleansUpTheHalfWrittenFolder()
    {
        using TempDir tmp = new();
        Zip(tmp.Sub("pack.zip"), ("a.txt", "one"), ("b.txt", "two"));

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        CommandResult<OpResult[]> result = await _actions
            .ExtractArchivesAsync(new([tmp.Sub("pack.zip")], tmp.Path), null, cancelled.Token);

        Assert.False(result.IsOk);
        Assert.Equal("cancelled", result.Reason);
        Assert.False(Directory.Exists(tmp.Sub("pack")));
    }

    [Fact]
    public void ZipTarAndTarGzAreOfferedAsArchives()
    {
        Assert.True(ArchiveService.IsArchive("holiday.ZIP"));
        Assert.True(ArchiveService.IsArchive("holiday.tar.gz"));
        Assert.True(ArchiveService.IsArchive("holiday.TAR"));
        Assert.True(ArchiveService.IsArchive("holiday.tgz"));
        Assert.False(ArchiveService.IsArchive("holiday.targz"));
        Assert.False(ArchiveService.IsArchive("holiday"));
    }

    [Fact]
    public void ATarGzUnpacksIntoAFolderNamedAfterTheArchive()
    {
        using TempDir tmp = new();
        TarBuilder.Make(tmp.Sub("pack.tar.gz"), gzip: true, ("a.txt", "one"), ("sub/b.txt", "two"));

        CommandResult<OpResult[]> result = _actions.ExtractArchives(new([tmp.Sub("pack.tar.gz")], tmp.Path));

        Assert.True(result.IsOk, result.Message);
        Assert.Equal("one", File.ReadAllText(tmp.Sub("pack", "a.txt")));
        Assert.Equal("two", File.ReadAllText(tmp.Sub("pack", "sub", "b.txt")));
    }

    [Fact]
    public void APlainTarUnpacksIntoAFolderNamedAfterTheArchive()
    {
        using TempDir tmp = new();
        TarBuilder.Make(tmp.Sub("pack.tar"), gzip: false, ("a.txt", "one"));

        CommandResult<OpResult[]> result = _actions.ExtractArchives(new([tmp.Sub("pack.tar")], tmp.Path));

        Assert.True(result.IsOk, result.Message);
        Assert.Equal("one", File.ReadAllText(tmp.Sub("pack", "a.txt")));
    }

    [Fact]
    public void ADamagedTarGzLeavesNothingBehind()
    {
        using TempDir tmp = new();
        tmp.File("pack.tar.gz", "this is not really a tar.gz");

        CommandResult<OpResult[]> result = _actions.ExtractArchives(new([tmp.Sub("pack.tar.gz")], tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("bad_archive", result.Reason);
        Assert.False(Directory.Exists(tmp.Sub("pack")));
    }
}

using System.IO.Compression;
using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class CompressTests
{
    private readonly Actions _actions = new();

    private static string[] Names(string archivePath)
    {
        using ZipArchive zip = ZipFile.OpenRead(archivePath);
        return [.. zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal)];
    }

    private static string Read(string archivePath, string entryName)
    {
        using ZipArchive zip = ZipFile.OpenRead(archivePath);
        using StreamReader reader = new(zip.GetEntry(entryName)!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void OneFileBecomesAZipNamedAfterIt()
    {
        using TempDir tmp = new();
        tmp.File("notes.txt", "hello");

        CommandResult<OpResult[]> result = _actions.CompressItems(new([tmp.Sub("notes.txt")], tmp.Path));

        Assert.True(result.IsOk);
        Assert.Equal(tmp.Sub("notes.zip"), result.Data![0].Item!.FullPath);
        Assert.Equal(["notes.txt"], Names(tmp.Sub("notes.zip")));
        Assert.Equal("hello", Read(tmp.Sub("notes.zip"), "notes.txt"));
    }

    [Fact]
    public void OneFolderKeepsItsNameAndEverythingUnderIt()
    {
        using TempDir tmp = new();
        tmp.Dir("project");
        tmp.Dir("project/src");
        tmp.File("project/readme.md", "read me");
        tmp.File("project/src/main.cs", "code");

        CommandResult<OpResult[]> result = _actions.CompressItems(new([tmp.Sub("project")], tmp.Path));

        Assert.True(result.IsOk);
        Assert.Equal(["project/readme.md", "project/src/main.cs"], Names(tmp.Sub("project.zip")));
        Assert.Equal("code", Read(tmp.Sub("project.zip"), "project/src/main.cs"));
    }

    [Fact]
    public void SeveralItemsGoIntoOneZipNamedAfterTheFolderTheyAreIn()
    {
        using TempDir tmp = new();
        string here = tmp.Dir("holiday");
        File.WriteAllText(Path.Combine(here, "one.txt"), "1");
        File.WriteAllText(Path.Combine(here, "two.txt"), "2");

        CommandResult<OpResult[]> result = _actions.CompressItems(
            new([Path.Combine(here, "one.txt"), Path.Combine(here, "two.txt")], here));

        Assert.True(result.IsOk);
        Assert.Equal(Path.Combine(here, "holiday.zip"), result.Data![0].Item!.FullPath);
        Assert.Equal(["one.txt", "two.txt"], Names(Path.Combine(here, "holiday.zip")));
    }

    [Fact]
    public void AnEmptyFolderIsStillInTheZip()
    {
        using TempDir tmp = new();
        tmp.Dir("shell");
        tmp.Dir("shell/nothing");
        tmp.File("shell/a.txt", "a");

        _actions.CompressItems(new([tmp.Sub("shell")], tmp.Path));

        Assert.Equal(["shell/a.txt", "shell/nothing/"], Names(tmp.Sub("shell.zip")));
    }

    [Fact]
    public void ASecondZipTakesTheNextFreeNameRatherThanOverwriting()
    {
        using TempDir tmp = new();
        tmp.File("notes.txt", "hello");

        _actions.CompressItems(new([tmp.Sub("notes.txt")], tmp.Path));
        CommandResult<OpResult[]> again = _actions.CompressItems(new([tmp.Sub("notes.txt")], tmp.Path));

        Assert.True(again.IsOk);
        Assert.True(File.Exists(tmp.Sub("notes.zip")));
        Assert.Equal(tmp.Sub("notes (2).zip"), again.Data![0].Item!.FullPath);
    }

    [Fact]
    public void ZippingNothingIsRefused()
    {
        using TempDir tmp = new();

        CommandResult<OpResult[]> result = _actions.CompressItems(new([], tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("nothing_to_do", result.Reason);
    }

    [Fact]
    public void AnItemThatIsNoLongerThereIsReportedAndLeavesNoZipBehind()
    {
        using TempDir tmp = new();

        CommandResult<OpResult[]> result = _actions.CompressItems(new([tmp.Sub("gone.txt")], tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
        Assert.Empty(Directory.GetFiles(tmp.Path));
    }

    [Fact]
    public async Task ACancelledRunTakesItsHalfWrittenZipWithIt()
    {
        using TempDir tmp = new();
        tmp.File("a.txt", "one");
        tmp.File("b.txt", "two");

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        CommandResult<OpResult[]> result = await _actions
            .CompressItemsAsync(new([tmp.Sub("a.txt"), tmp.Sub("b.txt")], tmp.Path), null, cancelled.Token);

        Assert.False(result.IsOk);
        Assert.Equal("cancelled", result.Reason);
        Assert.Empty(Directory.GetFiles(tmp.Path, "*.zip"));
    }

    [Fact]
    public void AZipRoveMadeIsAZipRoveCanUnpack()
    {
        using TempDir tmp = new();
        tmp.Dir("project");
        tmp.Dir("project/src");
        tmp.File("project/src/main.cs", "code");

        _actions.CompressItems(new([tmp.Sub("project")], tmp.Path));
        string away = tmp.Dir("elsewhere");
        CommandResult<OpResult[]> unpacked = _actions.ExtractArchives(new([tmp.Sub("project.zip")], away));

        Assert.True(unpacked.IsOk);
        Assert.Equal("code", File.ReadAllText(Path.Combine(away, "project", "project", "src", "main.cs")));
    }

    [Fact]
    public void ADeepPathIsPackedTheSameAsAnyOther()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();
        File.WriteAllText(LongPath.ForIo(Path.Combine(deep, "buried.txt")), "down here");

        CommandResult<OpResult[]> result = _actions.CompressItems(
            new([Path.Combine(deep, "buried.txt")], deep));

        Assert.True(result.IsOk);
        using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(Path.Combine(deep, "buried.zip")));
        Assert.Equal(["buried.txt"], zip.Entries.Select(e => e.FullName));
    }
}

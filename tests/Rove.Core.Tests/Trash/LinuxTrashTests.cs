using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

[Collection(XdgEnvironment.Name)]
public sealed class LinuxTrashTests : IDisposable
{
    private readonly string? _previousDataHome;
    private readonly TempDir _root = new();

    public LinuxTrashTests()
    {
        _previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _root.Dir("data"));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _previousDataHome);
        _root.Dispose();
    }

    private string TrashFiles => Path.Combine(_root.Sub("data"), "Trash", "files");
    private string TrashInfo => Path.Combine(_root.Sub("data"), "Trash", "info");

    private string InfoFor(string name) => Path.Combine(TrashInfo, name + ".trashinfo");

    [Fact]
    public void TrashingAFileMovesItOutOfTheFolderAndRecordsWhereItCameFrom()
    {
        string path = _root.File("notes.txt", "keep me");

        CommandResult<string?> result = LinuxTrash.MoveToTrash([path]);

        Assert.True(result.IsOk, result.Message);
        Assert.False(File.Exists(path));
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(TrashFiles, "notes.txt")));

        string info = File.ReadAllText(InfoFor("notes.txt"));
        Assert.StartsWith("[Trash Info]", info);
        Assert.Contains("DeletionDate=", info);
        Assert.Equal(path, LinuxTrash.ReadRecordedPath(InfoFor("notes.txt")));
    }

    [Fact]
    public void TrashingAFolderTakesEverythingInsideItAlong()
    {
        string folder = _root.Dir("project");
        File.WriteAllText(Path.Combine(folder, "inner.txt"), "content");

        CommandResult<string?> result = LinuxTrash.MoveToTrash([folder]);

        Assert.True(result.IsOk, result.Message);
        Assert.False(Directory.Exists(folder));
        Assert.Equal("content", File.ReadAllText(Path.Combine(TrashFiles, "project", "inner.txt")));
    }

    [Fact]
    public void RestoringPutsTheItemBackWhereItWas()
    {
        string path = _root.File("notes.txt", "keep me");
        LinuxTrash.MoveToTrash([path]);

        CommandResult<string[]> restored = LinuxTrash.RestoreFromTrash([path]);

        Assert.True(restored.IsOk);
        Assert.Equal([path], restored.Data!);
        Assert.Equal("keep me", File.ReadAllText(path));

        Assert.False(File.Exists(Path.Combine(TrashFiles, "notes.txt")));
        Assert.False(File.Exists(InfoFor("notes.txt")));
    }

    [Fact]
    public void TwoFilesWithTheSameNameBothFitAndBothGoHomeAgain()
    {
        string first = _root.File("report.txt", "first");
        string second = Path.Combine(_root.Dir("archive"), "report.txt");
        File.WriteAllText(second, "second");

        Assert.True(LinuxTrash.MoveToTrash([first]).IsOk);
        Assert.True(LinuxTrash.MoveToTrash([second]).IsOk);

        Assert.Equal("first", File.ReadAllText(Path.Combine(TrashFiles, "report.txt")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(TrashFiles, "report.1.txt")));

        CommandResult<string[]> restored = LinuxTrash.RestoreFromTrash([first, second]);

        Assert.Equal(2, restored.Data!.Length);
        Assert.Equal("first", File.ReadAllText(first));
        Assert.Equal("second", File.ReadAllText(second));
    }

    [Fact]
    public void RestoringWillNotWriteOverSomethingThatTookTheNameBack()
    {
        string path = _root.File("notes.txt", "the trashed one");
        LinuxTrash.MoveToTrash([path]);
        File.WriteAllText(path, "a newer file by the same name");

        CommandResult<string[]> restored = LinuxTrash.RestoreFromTrash([path]);

        Assert.Empty(restored.Data!);
        Assert.Equal("a newer file by the same name", File.ReadAllText(path));
        Assert.True(File.Exists(Path.Combine(TrashFiles, "notes.txt")));
    }

    [Fact]
    public void RestoringRebuildsAFolderThatWasRemovedInTheMeantime()
    {
        string folder = _root.Dir("gone");
        string path = Path.Combine(folder, "note.txt");
        File.WriteAllText(path, "hi");

        LinuxTrash.MoveToTrash([path]);
        Directory.Delete(folder);

        CommandResult<string[]> restored = LinuxTrash.RestoreFromTrash([path]);

        Assert.Equal([path], restored.Data!);
        Assert.Equal("hi", File.ReadAllText(path));
    }

    [Fact]
    public void SomethingThatWasNeverTrashedSimplyIsNotRestored()
    {
        CommandResult<string[]> restored = LinuxTrash.RestoreFromTrash([_root.Sub("imaginary.txt")]);

        Assert.True(restored.IsOk);
        Assert.Empty(restored.Data!);
    }

    [Fact]
    public void TrashingSomethingThatIsNotThereSaysSo()
    {
        CommandResult<string?> result = LinuxTrash.MoveToTrash([_root.Sub("imaginary.txt")]);

        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }

    [Fact]
    public void TheTrashIsBrowsedAtItsFilesDirectory()
    {
        Assert.Equal(TrashFiles, LinuxTrash.BrowsePath());
    }

    [Fact]
    public void PuttingBackFromInsideTheTrashSendsItHome()
    {
        string path = _root.File("notes.txt", "keep me");
        LinuxTrash.MoveToTrash([path]);

        OpResult[] results = LinuxTrash.RestoreTrashedPaths([Path.Combine(TrashFiles, "notes.txt")]);

        Assert.True(results[0].Ok, results[0].Message);
        Assert.Equal("keep me", File.ReadAllText(path));
        Assert.Equal(path, results[0].Item!.FullPath);
        Assert.False(File.Exists(InfoFor("notes.txt")));
    }

    [Fact]
    public void PuttingBackAFolderTakesEverythingInsideItAlong()
    {
        string folder = _root.Dir("project");
        File.WriteAllText(Path.Combine(folder, "inner.txt"), "content");
        LinuxTrash.MoveToTrash([folder]);

        OpResult[] results = LinuxTrash.RestoreTrashedPaths([Path.Combine(TrashFiles, "project")]);

        Assert.True(results[0].Ok, results[0].Message);
        Assert.Equal("content", File.ReadAllText(Path.Combine(folder, "inner.txt")));
    }

    [Fact]
    public void SomethingDeepInsideATrashedFolderCannotBePutBackOnItsOwn()
    {
        string folder = _root.Dir("project");
        File.WriteAllText(Path.Combine(folder, "inner.txt"), "content");
        LinuxTrash.MoveToTrash([folder]);

        OpResult[] results = LinuxTrash.RestoreTrashedPaths([Path.Combine(TrashFiles, "project", "inner.txt")]);

        Assert.False(results[0].Ok);
        Assert.Equal("not_in_trash", results[0].Reason);
    }

    [Fact]
    public void WithNoRecordThereIsNowhereToPutItBack()
    {
        string path = _root.File("notes.txt", "keep me");
        LinuxTrash.MoveToTrash([path]);
        File.Delete(InfoFor("notes.txt"));

        OpResult[] results = LinuxTrash.RestoreTrashedPaths([Path.Combine(TrashFiles, "notes.txt")]);

        Assert.False(results[0].Ok);
        Assert.Equal("no_record", results[0].Reason);
        Assert.True(File.Exists(Path.Combine(TrashFiles, "notes.txt")));
    }

    [Fact]
    public void PuttingBackWillNotWriteOverWhatIsThereNow()
    {
        string path = _root.File("notes.txt", "the trashed one");
        LinuxTrash.MoveToTrash([path]);
        File.WriteAllText(path, "a newer file by the same name");

        OpResult[] results = LinuxTrash.RestoreTrashedPaths([Path.Combine(TrashFiles, "notes.txt")]);

        Assert.False(results[0].Ok);
        Assert.Equal("already_exists", results[0].Reason);
        Assert.Equal("a newer file by the same name", File.ReadAllText(path));
        Assert.True(File.Exists(Path.Combine(TrashFiles, "notes.txt")));
    }

    [Fact]
    public void SomethingOutsideTheTrashIsNotSomethingToPutBack()
    {
        string path = _root.File("notes.txt", "still here");

        OpResult[] results = LinuxTrash.RestoreTrashedPaths([path]);

        Assert.False(results[0].Ok);
        Assert.Equal("not_in_trash", results[0].Reason);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DeletingSomethingOutOfTheTrashForGoodDropsItsRecordToo()
    {
        string path = _root.File("notes.txt", "keep me");
        LinuxTrash.MoveToTrash([path]);
        string inTrash = Path.Combine(TrashFiles, "notes.txt");

        File.Delete(inTrash);
        LinuxTrash.ForgetRecord(inTrash);

        Assert.False(File.Exists(InfoFor("notes.txt")));
    }

    [Fact]
    public void ForgettingLeavesRecordsOfThingsOutsideTheTrashAlone()
    {
        string path = _root.File("notes.txt", "keep me");
        LinuxTrash.MoveToTrash([path]);

        LinuxTrash.ForgetRecord(_root.Sub("somewhere", "notes.txt"));

        Assert.True(File.Exists(InfoFor("notes.txt")));
    }

    [Theory]
    [InlineData("/home/yeide/plain.txt")]
    [InlineData("/home/yeide/a file with spaces.txt")]
    [InlineData("/home/yeide/100% #1 & done.txt")]
    [InlineData("/home/yeide/Ünïcödé/naïve.txt")]
    public void TheRecordedPathSurvivesTheTripThroughTheInfoFile(string path)
    {
        Assert.Equal(path, LinuxTrash.DecodePath(LinuxTrash.EncodePath(path)));
    }

    [Fact]
    public void TheRecordedPathKeepsItsSlashesReadable()
    {
        Assert.Equal("/home/yeide/my%20file.txt", LinuxTrash.EncodePath("/home/yeide/my file.txt"));
    }

    [Fact]
    public void TheInfoFileIsShapedTheWayEveryOtherFileManagerReadsIt()
    {
        string info = LinuxTrash.InfoContents("/home/yeide/notes.txt", new DateTime(2026, 9, 9, 14, 5, 30));

        Assert.Equal(
            "[Trash Info]\nPath=/home/yeide/notes.txt\nDeletionDate=2026-09-09T14:05:30\n",
            info);
    }
}

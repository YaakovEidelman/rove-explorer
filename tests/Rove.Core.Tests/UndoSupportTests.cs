using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class DeleteIfEmptyTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void RemovesAFreshlyCreatedFile()
    {
        using TempDir tmp = new();
        string path = tmp.File("new.txt");

        CommandResult<string?> result = _actions.DeleteIfEmpty(new(path));

        Assert.True(result.IsOk);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void RemovesAFreshlyCreatedFolder()
    {
        using TempDir tmp = new();
        string path = tmp.Dir("new");

        CommandResult<string?> result = _actions.DeleteIfEmpty(new(path));

        Assert.True(result.IsOk);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void RefusesAFileThatHasBeenWrittenTo()
    {
        using TempDir tmp = new();
        string path = tmp.File("notes.txt", "work I did after creating it");

        CommandResult<string?> result = _actions.DeleteIfEmpty(new(path));

        Assert.False(result.IsOk);
        Assert.Equal("not_empty", result.Reason);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void RefusesAFolderThatHasSomethingInIt()
    {
        using TempDir tmp = new();
        string path = tmp.Dir("project");
        File.WriteAllText(Path.Combine(path, "file.txt"), "x");

        CommandResult<string?> result = _actions.DeleteIfEmpty(new(path));

        Assert.False(result.IsOk);
        Assert.Equal("not_empty", result.Reason);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void SaysNotFoundWhenTheItemIsAlreadyGone()
    {
        using TempDir tmp = new();

        CommandResult<string?> result = _actions.DeleteIfEmpty(new(tmp.Sub("never-existed")));

        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }
}

public class RemoveItemsTests
{
    private readonly Actions _actions = new();

    [Fact]
    public async Task TakesBackCopiesAPasteLeftBehind()
    {
        using TempDir tmp = new();
        string file = tmp.File("copy.txt", "hi");
        string folder = tmp.Dir("copied");
        File.WriteAllText(Path.Combine(folder, "inner.txt"), "hi");

        CommandResult<OpResult[]> result = await _actions.RemoveItemsAsync(new([file, folder]));

        Assert.True(result.IsOk, result.Message);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task ItemsAlreadyGoneAreNotAFailure()
    {
        using TempDir tmp = new();

        CommandResult<OpResult[]> result = await _actions.RemoveItemsAsync(new([tmp.Sub("gone.txt")]));

        Assert.True(result.IsOk);
    }
}

public class RestoreFromTrashTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void BringsADeletedFileBackToWhereItWas()
    {
        if (!TrashService.IsSupported)
            return; // the Recycle Bin is a Windows thing

        using TempDir tmp = new();
        string path = tmp.File("deleted-by-mistake.txt", "the contents");

        CommandResult<OpResult[]> deleted = _actions.DeleteItems(new([path]));
        Assert.True(deleted.IsOk, deleted.Message);
        Assert.False(File.Exists(path));

        CommandResult<OpResult[]> restored = _actions.RestoreItems(new([path]));

        Assert.True(restored.IsOk, restored.Message);
        Assert.True(File.Exists(path));
        Assert.Equal("the contents", File.ReadAllText(path));
    }

    [Fact]
    public void SomethingThatWasNeverDeletedComesBackAsNotRestored()
    {
        if (!TrashService.IsSupported)
            return; // the Recycle Bin is a Windows thing

        using TempDir tmp = new();

        CommandResult<OpResult[]> result = _actions.RestoreItems(new([tmp.Sub("never-trashed.txt")]));

        Assert.False(result.IsOk);
        Assert.Equal("not_restored", result.Reason);
    }
}

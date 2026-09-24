using Rove.Core.Endpoints;
using Rove.Core.Protocol;
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

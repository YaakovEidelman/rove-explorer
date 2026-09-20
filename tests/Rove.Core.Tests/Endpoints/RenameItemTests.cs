using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class RenameItemTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void RenamesFile()
    {
        using TempDir tmp = new();
        string src = tmp.File("old.txt", "data");

        CommandResult<FolderItem?> result = _actions.RenameItem(new(src, "new.txt"));

        Assert.True(result.IsOk);
        Assert.False(File.Exists(src));
        Assert.Equal("data", File.ReadAllText(tmp.Sub("new.txt")));
    }

    [Fact]
    public void RenamesDirectory()
    {
        using TempDir tmp = new();
        string src = tmp.Dir("olddir");
        tmp.File(@"olddir\inner.txt", "x");

        CommandResult<FolderItem?> result = _actions.RenameItem(new(src, "newdir"));

        Assert.True(result.IsOk);
        Assert.True(File.Exists(tmp.Sub("newdir", "inner.txt")));
    }

    [Fact]
    public void RenameOntoExistingFolder_FailsAndFolderSurvives()
    {
        using TempDir tmp = new();
        string file = tmp.File("victim.txt");
        tmp.Dir("target");
        tmp.File(@"target\important.txt", "do not lose me");

        CommandResult<FolderItem?> result = _actions.RenameItem(new(file, "target"));

        Assert.False(result.IsOk);
        Assert.Equal("already_exists", result.Reason);
        Assert.True(Directory.Exists(tmp.Sub("target")));
        Assert.Equal("do not lose me", File.ReadAllText(tmp.Sub("target", "important.txt")));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void RenameOntoExistingFile_Fails()
    {
        using TempDir tmp = new();
        string src = tmp.File("a.txt", "A");
        tmp.File("b.txt", "B");

        CommandResult<FolderItem?> result = _actions.RenameItem(new(src, "b.txt"));

        Assert.False(result.IsOk);
        Assert.Equal("B", File.ReadAllText(tmp.Sub("b.txt")));
    }

    [Fact]
    public void PathTraversalNewName_IsRejected()
    {
        using TempDir tmp = new();
        string src = tmp.File("a.txt");
        CommandResult<FolderItem?> result = _actions.RenameItem(new(src, @"..\stolen.txt"));
        Assert.False(result.IsOk);
        Assert.True(File.Exists(src));
    }
}

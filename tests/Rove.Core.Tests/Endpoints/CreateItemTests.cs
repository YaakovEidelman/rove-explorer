using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class CreateItemTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void CreatesFileAndFolder()
    {
        using TempDir tmp = new();

        CommandResult<FolderItem?> file = _actions.CreateItem(new(tmp.Path, "new.txt", IsDirectory: false));
        CommandResult<FolderItem?> folder = _actions.CreateItem(new(tmp.Path, "newdir", IsDirectory: true));

        Assert.True(file.IsOk);
        Assert.True(File.Exists(tmp.Sub("new.txt")));
        Assert.True(folder.IsOk);
        Assert.True(Directory.Exists(tmp.Sub("newdir")));
    }

    [Fact]
    public void ExistingFile_IsNotTruncated()
    {
        using TempDir tmp = new();
        tmp.File("keep.txt", "precious content");

        CommandResult<FolderItem?> result = _actions.CreateItem(new(tmp.Path, "keep.txt", IsDirectory: false));

        Assert.False(result.IsOk);
        Assert.Equal("already_exists", result.Reason);
        Assert.Equal("precious content", File.ReadAllText(tmp.Sub("keep.txt")));
    }

    [Fact]
    public void TraversalName_IsRejected()
    {
        using TempDir tmp = new();
        CommandResult<FolderItem?> result = _actions.CreateItem(new(tmp.Path, @"..\escape.txt", IsDirectory: false));
        Assert.False(result.IsOk);
        Assert.False(File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(tmp.Path)!, "escape.txt")));
    }
}

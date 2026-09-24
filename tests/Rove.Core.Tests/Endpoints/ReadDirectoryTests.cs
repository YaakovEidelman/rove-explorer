using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class ReadDirectoryTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void ListsFilesAndFolders_WithSizes()
    {
        using TempDir tmp = new();
        tmp.File("a.txt", "hello");
        tmp.Dir("sub");

        CommandResult<FolderItem[]> result = _actions.ReadDirectory(new(tmp.Path));

        Assert.True(result.IsOk);
        Assert.Equal(2, result.Data!.Length);
        FolderItem file = Assert.Single(result.Data, i => !i.IsDirectory);
        Assert.Equal(5, file.Size);
        Assert.Equal(".txt", file.Extension);
    }

    [Fact]
    public void MissingDirectory_FailsWithNotFound()
    {
        using TempDir tmp = new();
        CommandResult<FolderItem[]> result = _actions.ReadDirectory(new(tmp.Sub("nope")));
        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }
}

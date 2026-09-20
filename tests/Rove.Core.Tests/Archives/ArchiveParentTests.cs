using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

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

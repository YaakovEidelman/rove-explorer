using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class ArchiveResolvePathTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void ATypedPathToAFolderInsideTheZipGoesThere()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        CommandResult<FolderItem?> found = _actions.ResolvePath(new(tmp.Sub("pack.zip", "sub"), tmp.Path));

        Assert.True(found.IsOk, found.Message);
        Assert.True(found.Data!.IsDirectory);
        Assert.Equal(tmp.Sub("pack.zip", "sub"), found.Data.FullPath);
    }

    [Fact]
    public void ATypedPathToAFileInsideTheZipFindsTheFile()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        CommandResult<FolderItem?> found = _actions.ResolvePath(new(tmp.Sub("pack.zip", "sub", "b.txt"), tmp.Path));

        Assert.True(found.IsOk, found.Message);
        Assert.False(found.Data!.IsDirectory);
        Assert.Equal("b.txt", found.Data.Name);
    }

    [Fact]
    public void ATypedPathToNothingInsideTheZipIsNotFound()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<FolderItem?> found = _actions.ResolvePath(new(tmp.Sub("pack.zip", "nope.txt"), tmp.Path));

        Assert.False(found.IsOk);
        Assert.Equal("not_found", found.Reason);
    }

    [Fact]
    public void ATypedPathToTheZipItselfStillFindsTheFileOnDisk()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<FolderItem?> found = _actions.ResolvePath(new(tmp.Sub("pack.zip"), tmp.Path));

        Assert.True(found.IsOk);
        Assert.False(found.Data!.IsDirectory);
    }
}

using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class ResolvePathEndpointTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void AFolderComesBackAsADirectory()
    {
        using TempDir tmp = new();
        tmp.Dir("sub");

        CommandResult<FolderItem?> result = _actions.ResolvePath(new("sub", tmp.Path));

        Assert.True(result.IsOk);
        Assert.True(result.Data!.IsDirectory);
        Assert.True(PathCompare.PathMatches(tmp.Sub("sub"), result.Data.FullPath));
    }

    [Fact]
    public void AFileComesBackAsAFile()
    {
        using TempDir tmp = new();
        tmp.File("notes.txt", "hello");

        CommandResult<FolderItem?> result = _actions.ResolvePath(new("notes.txt", tmp.Path));

        Assert.True(result.IsOk);
        Assert.False(result.Data!.IsDirectory);
    }

    [Fact]
    public void APathWithNothingAtItFails()
    {
        using TempDir tmp = new();

        CommandResult<FolderItem?> result = _actions.ResolvePath(new("nowhere", tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }

    [Fact]
    public void TextThatIsNoPathAtAllFails()
    {
        using TempDir tmp = new();

        CommandResult<FolderItem?> result = _actions.ResolvePath(new("   ", tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("invalid_path", result.Reason);
    }

    [Fact]
    public void ALongPathStillResolves()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();

        CommandResult<FolderItem?> result = _actions.ResolvePath(new(deep, tmp.Path));

        Assert.True(result.IsOk);
        Assert.True(result.Data!.IsDirectory);
    }
}

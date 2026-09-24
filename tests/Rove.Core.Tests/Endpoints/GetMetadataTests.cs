using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class GetMetadataTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void FileMetadata_HasExactSizeAndTimestamps()
    {
        using TempDir tmp = new();
        string file = tmp.File("meta.txt", "12345");

        CommandResult<ItemMetadata?> result = _actions.GetMetadata(new(file));

        Assert.True(result.IsOk);
        ItemMetadata m = result.Data!;
        Assert.False(m.IsDirectory);
        Assert.Equal(5, m.SizeBytes);
        Assert.Equal(".txt", m.Extension);
        Assert.True(m.ModifiedUtc > DateTime.UtcNow.AddMinutes(-5));
        Assert.Null(m.FileCount);
    }

    [Fact]
    public void DirectoryMetadata_CountsChildrenAndTotalSize()
    {
        using TempDir tmp = new();
        string dir = tmp.Dir("stats");
        tmp.File(@"stats\a.txt", "aa");
        tmp.File(@"stats\b.txt", "bbb");
        tmp.Dir(@"stats\subdir");

        CommandResult<ItemMetadata?> result = _actions.GetMetadata(new(dir));

        Assert.True(result.IsOk);
        ItemMetadata m = result.Data!;
        Assert.True(m.IsDirectory);
        Assert.Equal(2, m.FileCount);
        Assert.Equal(1, m.DirectoryCount);
        Assert.Equal(5, m.TotalSizeBytes);
        Assert.False(m.Truncated);
    }

    [Fact]
    public void DirectoryMetadata_SkipsInaccessibleSubdirButCountsSiblings()
    {
        if (OperatingSystem.IsWindows())
            return;

        using TempDir tmp = new();
        string dir = tmp.Dir("stats");
        tmp.File(@"stats\a.txt", "aa");
        string blocked = tmp.Dir(@"stats\blocked");
        tmp.File(@"stats\blocked\hidden.txt", "hidden");

        File.SetUnixFileMode(blocked, UnixFileMode.None);
        try
        {
            CommandResult<ItemMetadata?> result = _actions.GetMetadata(new(dir));

            Assert.True(result.IsOk);
            ItemMetadata m = result.Data!;
            Assert.Equal(1, m.FileCount);
            Assert.Equal(1, m.DirectoryCount);
            Assert.Equal(2, m.TotalSizeBytes);
        }
        finally
        {
            File.SetUnixFileMode(blocked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void FileMetadata_HasAUnixModeOnEveryPlatformButWindows()
    {
        using TempDir tmp = new();
        string file = tmp.File("meta.txt", "12345");

        CommandResult<ItemMetadata?> result = _actions.GetMetadata(new(file));

        ItemMetadata m = result.Data!;
        Assert.Equal(OperatingSystem.IsWindows(), m.UnixMode is null);
    }

    [Fact]
    public void MissingItem_FailsWithNotFound()
    {
        using TempDir tmp = new();
        CommandResult<ItemMetadata?> result = _actions.GetMetadata(new(tmp.Sub("nope.txt")));
        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }
}

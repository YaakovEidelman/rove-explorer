using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class DuplicateItemsTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void DuplicatesAFileNextToItselfWithACopySuffix()
    {
        using TempDir tmp = new();
        string file = tmp.File("report.txt", "hello");

        CommandResult<OpResult[]> result = _actions.DuplicateItems(new([file]));

        Assert.True(result.IsOk, result.Message);
        string copy = tmp.Sub("report (copy).txt");
        Assert.True(File.Exists(copy));
        Assert.Equal("hello", File.ReadAllText(copy));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void DuplicatesADirectoryRecursively()
    {
        using TempDir tmp = new();
        string dir = tmp.Dir("Notes");
        tmp.File(@"Notes\leaf.txt", "green");

        CommandResult<OpResult[]> result = _actions.DuplicateItems(new([dir]));

        Assert.True(result.IsOk, result.Message);
        Assert.Equal("green", File.ReadAllText(tmp.Sub("Notes (copy)", "leaf.txt")));
    }

    [Fact]
    public void RepeatedDuplicatesCountUp()
    {
        using TempDir tmp = new();
        string file = tmp.File("report.txt", "v1");

        _actions.DuplicateItems(new([file]));
        _actions.DuplicateItems(new([file]));
        _actions.DuplicateItems(new([file]));

        Assert.True(File.Exists(tmp.Sub("report (copy).txt")));
        Assert.True(File.Exists(tmp.Sub("report (copy 2).txt")));
        Assert.True(File.Exists(tmp.Sub("report (copy 3).txt")));
    }

    [Fact]
    public void MissingSourceFails()
    {
        using TempDir tmp = new();
        string missing = tmp.Sub("ghost.txt");

        CommandResult<OpResult[]> result = _actions.DuplicateItems(new([missing]));

        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Data![0].Reason);
    }
}

using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class RestoreFromTrashTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void BringsADeletedFileBackToWhereItWas()
    {
        if (!TrashService.IsSupported)
            return;

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
            return;

        using TempDir tmp = new();

        CommandResult<OpResult[]> result = _actions.RestoreItems(new([tmp.Sub("never-trashed.txt")]));

        Assert.False(result.IsOk);
        Assert.Equal("not_restored", result.Reason);
    }
}

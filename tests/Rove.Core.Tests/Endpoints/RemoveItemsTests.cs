using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class RemoveItemsTests
{
    private readonly Actions _actions = new();

    [Fact]
    public async Task TakesBackCopiesAPasteLeftBehind()
    {
        using TempDir tmp = new();
        string file = tmp.File("copy.txt", "hi");
        string folder = tmp.Dir("copied");
        File.WriteAllText(Path.Combine(folder, "inner.txt"), "hi");

        CommandResult<OpResult[]> result = await _actions.RemoveItemsAsync(new([file, folder]));

        Assert.True(result.IsOk, result.Message);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task ItemsAlreadyGoneAreNotAFailure()
    {
        using TempDir tmp = new();

        CommandResult<OpResult[]> result = await _actions.RemoveItemsAsync(new([tmp.Sub("gone.txt")]));

        Assert.True(result.IsOk);
    }
}

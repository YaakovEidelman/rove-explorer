using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class DeleteItemsPermanentTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void DeletesFilesAndFolders()
    {
        using TempDir tmp = new();
        string file = tmp.File("gone.txt");
        string dir = tmp.Dir("gonedir");
        tmp.File(@"gonedir\inner.txt");

        CommandResult<OpResult[]> result = _actions.DeleteItemsPermanent(new([file, dir]));

        Assert.True(result.IsOk);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void MissingItem_ReportsPerItemFailure_OthersProceed()
    {
        using TempDir tmp = new();
        string real = tmp.File("real.txt");
        string ghost = tmp.Sub("ghost.txt");

        CommandResult<OpResult[]> result = _actions.DeleteItemsPermanent(new([ghost, real]));

        Assert.False(result.IsOk);
        Assert.Equal("partial_failure", result.Reason);
        Assert.Single(result.Data!, r => !r.Ok);
        Assert.False(File.Exists(real));
    }
}

using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class MoveItemsTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void MovesMultipleItems_IntoTargetKeepingNames()
    {
        using TempDir tmp = new();
        string f1 = tmp.File("one.txt", "1");
        string f2 = tmp.File("two.txt", "2");
        string target = tmp.Dir("dest");

        CommandResult<OpResult[]> result = _actions.MoveItems(new([f1, f2], target, Overwrite: false));

        Assert.True(result.IsOk);
        Assert.All(result.Data!, op => Assert.True(op.Ok));
        Assert.Equal("1", File.ReadAllText(tmp.Sub("dest", "one.txt")));
        Assert.Equal("2", File.ReadAllText(tmp.Sub("dest", "two.txt")));
        Assert.False(File.Exists(f1));
    }

    [Fact]
    public void Collision_FailsThatItemOnly_AndDestinationSurvives()
    {
        using TempDir tmp = new();
        string source = tmp.File("clash.txt", "new");
        string ok = tmp.File("fine.txt", "fine");
        string target = tmp.Dir("dest");
        tmp.File(@"dest\clash.txt", "existing");

        CommandResult<OpResult[]> result = _actions.MoveItems(new([source, ok], target, Overwrite: false));

        Assert.False(result.IsOk);
        OpResult clash = Assert.Single(result.Data!, r => !r.Ok);
        Assert.Equal("already_exists", clash.Reason);
        Assert.Equal("existing", File.ReadAllText(tmp.Sub("dest", "clash.txt")));
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(tmp.Sub("dest", "fine.txt")));
    }

    [Fact]
    public void OverwriteFlag_ReplacesExistingFile()
    {
        using TempDir tmp = new();
        string source = tmp.File("clash.txt", "new");
        string target = tmp.Dir("dest");
        tmp.File(@"dest\clash.txt", "old");

        CommandResult<OpResult[]> result = _actions.MoveItems(new([source], target, Overwrite: true));

        Assert.True(result.IsOk);
        Assert.Equal("new", File.ReadAllText(tmp.Sub("dest", "clash.txt")));
    }

    [Fact]
    public void MovingFolderIntoItself_Fails()
    {
        using TempDir tmp = new();
        string dir = tmp.Dir("outer");
        string inner = tmp.Dir(@"outer\inner");

        CommandResult<OpResult[]> result = _actions.MoveItems(new([dir], inner, Overwrite: false));

        Assert.False(result.IsOk);
        Assert.Equal("invalid_target", result.Data![0].Reason);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void MoveDirectory_CarriesContents()
    {
        using TempDir tmp = new();
        string dir = tmp.Dir("box");
        tmp.File(@"box\thing.txt", "cargo");
        string target = tmp.Dir("dest");

        CommandResult<OpResult[]> result = _actions.MoveItems(new([dir], target, Overwrite: false));

        Assert.True(result.IsOk);
        Assert.Equal("cargo", File.ReadAllText(tmp.Sub("dest", "box", "thing.txt")));
        Assert.False(Directory.Exists(dir));
    }
}

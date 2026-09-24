using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class CopyItemsTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void CopiesFileAndDirectoryRecursively()
    {
        using TempDir tmp = new();
        string file = tmp.File("solo.txt", "s");
        string dir = tmp.Dir("tree");
        tmp.Dir(@"tree\branch");
        tmp.File(@"tree\branch\leaf.txt", "green");
        string target = tmp.Dir("dest");

        CommandResult<OpResult[]> result = _actions.CopyItems(new([file, dir], target, Overwrite: false));

        Assert.True(result.IsOk);
        Assert.Equal("s", File.ReadAllText(tmp.Sub("dest", "solo.txt")));
        Assert.Equal("green", File.ReadAllText(tmp.Sub("dest", "tree", "branch", "leaf.txt")));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void CopyCollision_FailsWithoutTouchingDestination()
    {
        using TempDir tmp = new();
        string source = tmp.File("clash.txt", "new");
        string target = tmp.Dir("dest");
        tmp.File(@"dest\clash.txt", "existing");

        CommandResult<OpResult[]> result = _actions.CopyItems(new([source], target, Overwrite: false));

        Assert.False(result.IsOk);
        Assert.Equal("existing", File.ReadAllText(tmp.Sub("dest", "clash.txt")));
    }

    [Fact]
    public void CopyingFolderIntoItself_Fails()
    {
        using TempDir tmp = new();
        string dir = tmp.Dir("outer");

        CommandResult<OpResult[]> result = _actions.CopyItems(new([dir], dir, Overwrite: false));

        Assert.False(result.IsOk);
        Assert.Equal("invalid_target", result.Data![0].Reason);
    }

    private static bool TryLinkDir(string path, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryLinkFile(string path, string target)
    {
        try
        {
            File.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [Fact]
    public void CopyingASelfReferencingSymlink_DoesNotLoop()
    {
        using TempDir tmp = new();
        string outer = tmp.Dir("outer");
        string loop = tmp.Sub("outer", "loop");
        if (!TryLinkDir(loop, outer))
            return;
        string target = tmp.Dir("dest");

        CommandResult<OpResult[]> result = _actions.CopyItems(new([outer], target, Overwrite: false));

        Assert.True(result.IsOk, result.Message);
        string copiedLink = tmp.Sub("dest", "outer", "loop");
        Assert.NotNull(new DirectoryInfo(copiedLink).LinkTarget);
    }

    [Fact]
    public void CopyingAFolderWithADirectorySymlinkInside_RecreatesTheLinkInsteadOfItsContents()
    {
        using TempDir tmp = new();
        string outer = tmp.Dir("outer");
        tmp.Dir("elsewhere");
        tmp.File(@"elsewhere\secret.txt", "hush");
        string link = tmp.Sub("outer", "link");
        if (!TryLinkDir(link, tmp.Sub("elsewhere")))
            return;
        string target = tmp.Dir("dest");

        CommandResult<OpResult[]> result = _actions.CopyItems(new([outer], target, Overwrite: false));

        Assert.True(result.IsOk, result.Message);
        string copiedLink = tmp.Sub("dest", "outer", "link");
        Assert.NotNull(new DirectoryInfo(copiedLink).LinkTarget);
    }

    [Fact]
    public void CopyingAFolderWithAFileSymlinkInside_RecreatesTheLinkInsteadOfCopyingContent()
    {
        using TempDir tmp = new();
        string outer = tmp.Dir("outer");
        string data = tmp.File(@"outer\data.txt", "real content");
        string link = tmp.Sub("outer", "link.txt");
        if (!TryLinkFile(link, data))
            return;
        string target = tmp.Dir("dest");

        CommandResult<OpResult[]> result = _actions.CopyItems(new([outer], target, Overwrite: false));

        Assert.True(result.IsOk, result.Message);
        string copiedLink = tmp.Sub("dest", "outer", "link.txt");
        Assert.NotNull(new FileInfo(copiedLink).LinkTarget);
    }
}

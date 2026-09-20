using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class ReadDirectoryTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void ListsFilesAndFolders_WithSizes()
    {
        using TempDir tmp = new();
        tmp.File("a.txt", "hello");
        tmp.Dir("sub");

        CommandResult<FolderItem[]> result = _actions.ReadDirectory(new(tmp.Path));

        Assert.True(result.IsOk);
        Assert.Equal(2, result.Data!.Length);
        FolderItem file = Assert.Single(result.Data, i => !i.IsDirectory);
        Assert.Equal(5, file.Size);
        Assert.Equal(".txt", file.Extension);
    }

    [Fact]
    public void MissingDirectory_FailsWithNotFound()
    {
        using TempDir tmp = new();
        CommandResult<FolderItem[]> result = _actions.ReadDirectory(new(tmp.Sub("nope")));
        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }
}

public class ListDrivesTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void ListsAtLeastTheDriveTheTestsRunFrom()
    {
        CommandResult<DriveEntry[]> result = _actions.ListDrives(new());

        Assert.True(result.IsOk);
        Assert.NotEmpty(result.Data!);
        string root = Path.GetPathRoot(Path.GetFullPath(Directory.GetCurrentDirectory()))!;
        Assert.Contains(result.Data!, d => PathCompare.PathMatches(d.RootPath, root));
    }

    [Fact]
    public void EveryListedDriveRootIsAReadableDirectory()
    {
        CommandResult<DriveEntry[]> result = _actions.ListDrives(new());

        foreach (DriveEntry drive in result.Data!)
            Assert.True(Directory.Exists(drive.RootPath), drive.RootPath);
    }
}

public class CreateItemTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void CreatesFileAndFolder()
    {
        using TempDir tmp = new();

        CommandResult<FolderItem?> file = _actions.CreateItem(new(tmp.Path, "new.txt", IsDirectory: false));
        CommandResult<FolderItem?> folder = _actions.CreateItem(new(tmp.Path, "newdir", IsDirectory: true));

        Assert.True(file.IsOk);
        Assert.True(File.Exists(tmp.Sub("new.txt")));
        Assert.True(folder.IsOk);
        Assert.True(Directory.Exists(tmp.Sub("newdir")));
    }

    [Fact]
    public void ExistingFile_IsNotTruncated()
    {
        using TempDir tmp = new();
        tmp.File("keep.txt", "precious content");

        CommandResult<FolderItem?> result = _actions.CreateItem(new(tmp.Path, "keep.txt", IsDirectory: false));

        Assert.False(result.IsOk);
        Assert.Equal("already_exists", result.Reason);
        Assert.Equal("precious content", File.ReadAllText(tmp.Sub("keep.txt")));
    }

    [Fact]
    public void TraversalName_IsRejected()
    {
        using TempDir tmp = new();
        CommandResult<FolderItem?> result = _actions.CreateItem(new(tmp.Path, @"..\escape.txt", IsDirectory: false));
        Assert.False(result.IsOk);
        Assert.False(File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(tmp.Path)!, "escape.txt")));
    }
}

public class RenameItemTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void RenamesFile()
    {
        using TempDir tmp = new();
        string src = tmp.File("old.txt", "data");

        CommandResult<FolderItem?> result = _actions.RenameItem(new(src, "new.txt"));

        Assert.True(result.IsOk);
        Assert.False(File.Exists(src));
        Assert.Equal("data", File.ReadAllText(tmp.Sub("new.txt")));
    }

    [Fact]
    public void RenamesDirectory()
    {
        using TempDir tmp = new();
        string src = tmp.Dir("olddir");
        tmp.File(@"olddir\inner.txt", "x");

        CommandResult<FolderItem?> result = _actions.RenameItem(new(src, "newdir"));

        Assert.True(result.IsOk);
        Assert.True(File.Exists(tmp.Sub("newdir", "inner.txt")));
    }

    [Fact]
    public void RenameOntoExistingFolder_FailsAndFolderSurvives()
    {
        using TempDir tmp = new();
        string file = tmp.File("victim.txt");
        tmp.Dir("target");
        tmp.File(@"target\important.txt", "do not lose me");

        CommandResult<FolderItem?> result = _actions.RenameItem(new(file, "target"));

        Assert.False(result.IsOk);
        Assert.Equal("already_exists", result.Reason);
        Assert.True(Directory.Exists(tmp.Sub("target")));
        Assert.Equal("do not lose me", File.ReadAllText(tmp.Sub("target", "important.txt")));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void RenameOntoExistingFile_Fails()
    {
        using TempDir tmp = new();
        string src = tmp.File("a.txt", "A");
        tmp.File("b.txt", "B");

        CommandResult<FolderItem?> result = _actions.RenameItem(new(src, "b.txt"));

        Assert.False(result.IsOk);
        Assert.Equal("B", File.ReadAllText(tmp.Sub("b.txt")));
    }

    [Fact]
    public void PathTraversalNewName_IsRejected()
    {
        using TempDir tmp = new();
        string src = tmp.File("a.txt");
        CommandResult<FolderItem?> result = _actions.RenameItem(new(src, @"..\stolen.txt"));
        Assert.False(result.IsOk);
        Assert.True(File.Exists(src));
    }
}

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

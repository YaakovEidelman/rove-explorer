using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class LongPathTests
{
    [Fact]
    public void ShortPathsAreLeftAlone()
    {
        Assert.Equal(@"C:\temp\a.txt", LongPath.ForIo(@"C:\temp\a.txt"));
    }

    [Fact]
    public void LongPathGetsTheExtendedPrefixOnWindows()
    {
        string deep = @"C:\" + string.Join(@"\", Enumerable.Repeat(new string('d', 40), 9));
        string io = LongPath.ForIo(deep);

        if (OperatingSystem.IsWindows())
            Assert.Equal(@"\\?\" + deep, io);
        else
            Assert.Equal(deep, io);
    }

    [Fact]
    public void DisplayStripsThePrefixBackOff()
    {
        Assert.Equal(@"C:\a\b", LongPath.Display(@"\\?\C:\a\b"));
        Assert.Equal(@"\\server\share\a", LongPath.Display(@"\\?\UNC\server\share\a"));
        Assert.Equal(@"C:\a\b", LongPath.Display(@"C:\a\b"));
    }

    [Fact]
    public void AlreadyExtendedPathsAreNotPrefixedTwice()
    {
        Assert.Equal(@"\\?\C:\a", LongPath.ForIo(@"\\?\C:\a"));
    }

    [Fact]
    public void PathsAreComparedByTheirPlainForm()
    {
        Assert.True(PathCompare.PathMatches(@"\\?\C:\a\b", @"C:\a\b"));
    }
}

public class LongPathActionsTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void ReadsAndCreatesInsideADeepDirectory()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();

        CommandResult<FolderItem?> created = _actions.CreateItem(new(deep, "note.txt", IsDirectory: false));
        Assert.True(created.IsOk, created.Message);

        CommandResult<FolderItem[]> listed = _actions.ReadDirectory(new(deep));
        Assert.True(listed.IsOk, listed.Message);
        FolderItem item = Assert.Single(listed.Data!);
        Assert.Equal("note.txt", item.Name);
    }

    [Fact]
    public void ItemsFromADeepDirectoryDoNotCarryThePrefix()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();
        _actions.CreateItem(new(deep, "note.txt", IsDirectory: false));

        FolderItem item = Assert.Single(_actions.ReadDirectory(new(deep)).Data!);

        Assert.False(item.FullPath.StartsWith(@"\\?\", StringComparison.Ordinal));
        Assert.StartsWith(tmp.Path, item.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CopiesAndPermanentlyDeletesADeepFile()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();
        string target = tmp.Dir("target");
        _actions.CreateItem(new(deep, "note.txt", IsDirectory: false));
        string source = Path.Combine(deep, "note.txt");

        CommandResult<OpResult[]> copied = _actions.CopyItems(new([source], target, Overwrite: false));
        Assert.True(copied.IsOk, copied.Message);
        Assert.True(File.Exists(Path.Combine(target, "note.txt")));

        CommandResult<OpResult[]> deleted = _actions.DeleteItemsPermanent(new([source]));
        Assert.True(deleted.IsOk, deleted.Message);
        Assert.False(File.Exists(LongPath.ForIo(source)));
    }

    [Fact]
    public void RenamesADeepFile()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();
        _actions.CreateItem(new(deep, "before.txt", IsDirectory: false));

        CommandResult<FolderItem?> renamed = _actions.RenameItem(new(Path.Combine(deep, "before.txt"), "after.txt"));

        Assert.True(renamed.IsOk, renamed.Message);
        Assert.Equal("after.txt", renamed.Data!.Name);
        Assert.True(File.Exists(LongPath.ForIo(Path.Combine(deep, "after.txt"))));
    }

    [Fact]
    public void MetadataReadsADeepFile()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();
        _actions.CreateItem(new(deep, "note.txt", IsDirectory: false));

        CommandResult<ItemMetadata?> meta = _actions.GetMetadata(new(Path.Combine(deep, "note.txt")));

        Assert.True(meta.IsOk, meta.Message);
        Assert.Equal("note.txt", meta.Data!.Name);
        Assert.False(meta.Data.FullPath.StartsWith(@"\\?\", StringComparison.Ordinal));
    }

    [Fact]
    public void RecycleBinSaysWhyItCannotTakeADeepPath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using TempDir tmp = new();
        string deep = tmp.DeepDir();
        _actions.CreateItem(new(deep, "note.txt", IsDirectory: false));

        CommandResult<OpResult[]> result = _actions.DeleteItems(new([Path.Combine(deep, "note.txt")]));

        Assert.False(result.IsOk);
        Assert.Equal("path_too_long", result.Reason);
    }
}

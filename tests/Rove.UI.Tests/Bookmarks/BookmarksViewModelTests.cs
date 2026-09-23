using Rove.Core;
using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class BookmarksViewModelTests : IDisposable
{
    private readonly string _bookmarkFile = Path.Combine(
        Path.GetTempPath(), "rove-marks-" + Guid.NewGuid().ToString("N") + ".json");

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "rove-bookmark-add-" + Guid.NewGuid().ToString("N"));

    private readonly List<RoveCore> _cores = [];

    private (BookmarksViewModel Bookmarks, CommandRegistry Registry) New()
    {
        CommandRegistry registry = new(KeymapLoad.Empty);
        RoveCore core = new();
        _cores.Add(core);
        BookmarksViewModel bookmarks = new(registry, new BookmarkStore(_bookmarkFile), core);
        return (bookmarks, registry);
    }

    [Fact]
    public void TheAddRowIsPinnedAtTheTopOfTheList()
    {
        (BookmarksViewModel bookmarks, _) = New();

        bookmarks.Toggle();

        Assert.True(bookmarks.Items[0].IsAddNew);
    }

    [Fact]
    public void TypingAPathAndApplyingAddsABookmarkForIt()
    {
        Directory.CreateDirectory(_root);
        (BookmarksViewModel bookmarks, CommandRegistry registry) = New();
        bookmarks.Toggle();

        bookmarks.GoToSelected();
        Assert.True(bookmarks.InAddBookmark);

        bookmarks.AddBookmarkPath = _root;
        registry.TryExecute(CommandDef.ApplyAddBookmark.Id);

        Assert.False(bookmarks.InAddBookmark);
        Assert.False(bookmarks.IsEmpty);
        BookmarkRow added = Assert.Single(bookmarks.Items, r => !r.IsAddNew);
        Assert.Equal(_root, added.Entry!.Bookmark.Path);
        Assert.True(added.Entry.Bookmark.IsDirectory);
    }

    [Fact]
    public void AddingAPathThatDoesNotExistLeavesAddModeOpenAndAddsNothing()
    {
        (BookmarksViewModel bookmarks, CommandRegistry registry) = New();
        bookmarks.Toggle();
        bookmarks.GoToSelected();

        bookmarks.AddBookmarkPath = Path.Combine(_root, "does-not-exist");
        string? info = null;
        bookmarks.InfoRaised += m => info = m;
        registry.TryExecute(CommandDef.ApplyAddBookmark.Id);

        Assert.True(bookmarks.InAddBookmark);
        Assert.True(bookmarks.IsEmpty);
        Assert.NotNull(info);
    }

    [Fact]
    public void CancelingAddModeLeavesTheListUnchanged()
    {
        (BookmarksViewModel bookmarks, CommandRegistry registry) = New();
        bookmarks.Toggle();
        bookmarks.GoToSelected();

        registry.TryExecute(CommandDef.CancelAddBookmark.Id);

        Assert.False(bookmarks.InAddBookmark);
        Assert.True(bookmarks.IsEmpty);
    }

    public void Dispose()
    {
        foreach (RoveCore core in _cores)
            core.Dispose();
        try
        {
            File.Delete(_bookmarkFile);
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

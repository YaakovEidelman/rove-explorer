using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class BookmarkStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(), "rove-marks-" + Guid.NewGuid().ToString("N") + ".json");

    private BookmarkStore Store() => new(_file);

    private static Bookmark Mark(string name) =>
        new(Path.Combine(Path.GetTempPath(), name), name, true);

    [Fact]
    public void BookmarkingSomethingTwiceTakesItBackOut()
    {
        BookmarkStore store = Store();
        Bookmark src = Mark("src");

        Assert.True(store.Toggle(src));
        Assert.True(store.Contains(src.Path));

        Assert.False(store.Toggle(src));
        Assert.False(store.Contains(src.Path));
        Assert.Empty(store.Items);
    }

    [Fact]
    public void TheListKeepsTheOrderThingsWereBookmarkedIn()
    {
        BookmarkStore store = Store();
        store.Toggle(Mark("one"));
        store.Toggle(Mark("two"));
        store.Toggle(Mark("three"));

        Assert.Equal(["one", "two", "three"], store.Items.Select(b => b.Name));
    }

    [Fact]
    public void BookmarksComeBackOnTheNextLaunch()
    {
        BookmarkStore first = Store();
        first.Toggle(Mark("keep"));

        BookmarkStore second = Store();

        Assert.Equal(["keep"], second.Items.Select(b => b.Name));
    }

    [Fact]
    public void AMissingOrRuinedFileReadsAsNoBookmarks()
    {
        Assert.Empty(Store().Items);

        File.WriteAllText(_file, "{ not json at all");

        Assert.Empty(Store().Items);
    }

    [Fact]
    public void TheFirstNineGetAKeyAndTheRestDoNot()
    {
        Assert.Equal("Ctrl+1", BookmarkStore.ShortcutFor(0));
        Assert.Equal("Ctrl+9", BookmarkStore.ShortcutFor(8));
        Assert.Equal("", BookmarkStore.ShortcutFor(9));
        Assert.Equal("", BookmarkStore.ShortcutFor(-1));
    }

    [Fact]
    public void RemovingOneMovesTheKeysBelowItUp()
    {
        BookmarkStore store = Store();
        store.Toggle(Mark("one"));
        store.Toggle(Mark("two"));

        Assert.Equal(1, store.IndexOf(Mark("two").Path));

        store.Remove(Mark("one").Path);

        Assert.Equal(0, store.IndexOf(Mark("two").Path));
    }

    [Fact]
    public void RemovingSomethingThatWasNeverBookmarkedChangesNothing()
    {
        BookmarkStore store = Store();
        store.Toggle(Mark("one"));

        Assert.False(store.Remove(Mark("elsewhere").Path));
        Assert.Single(store.Items);
    }

    [Fact]
    public void EveryChangeIsAnnounced()
    {
        BookmarkStore store = Store();
        int changes = 0;
        store.Changed += () => changes++;

        store.Toggle(Mark("one"));
        store.Toggle(Mark("one"));
        store.Remove(Mark("gone").Path);

        Assert.Equal(2, changes);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
        }
    }
}

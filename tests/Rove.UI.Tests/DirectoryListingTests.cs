using Avalonia.Media;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Models;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

/// <summary>No real icons: the rows only need something that answers.</summary>
internal sealed class NullIconCache : IIconCache
{
    public Task<IImage?> GetIconAsync(FolderItem item, int size) => Task.FromResult<IImage?>(null);

    public bool TryGetIcon(FolderItem item, int size, out IImage? icon)
    {
        icon = null;
        return true; // "already resolved, to nothing" — keeps rows off the async path
    }
}

public class DirectoryListingTests
{
    /// <summary>The folder these rows live in, spelled the way this OS spells one.</summary>
    private static readonly string _root = OperatingSystem.IsWindows() ? @"C:\test" : "/test";

    private static string At(string name) => Path.Combine(_root, name);

    private static FolderItem Item(string name, bool isDirectory = false) =>
        new(name, At(name), FileAttributes.Normal, DateTime.UnixEpoch, isDirectory,
            isDirectory ? null : 1, isDirectory ? "" : Path.GetExtension(name));

    private static DirectoryListing Listing(params FolderItem[] items)
    {
        DirectoryListing listing = new(new NullIconCache()) { CurrentDir = _root };
        listing.Load(_root, items);
        return listing;
    }

    private static string[] Names(DirectoryListing listing) => [.. listing.Items.Select(i => i.Name)];

    private static FolderItem Hidden(string name) =>
        Item(name) with { Attributes = FileAttributes.Hidden };

    private static FolderItem SystemItem(string name) =>
        Item(name) with { Attributes = FileAttributes.System };

    [Fact]
    public void HiddenItemsStayOutOfTheListUntilAskedFor()
    {
        DirectoryListing listing = Listing(Item("notes.txt"), Hidden(".bashrc"), Hidden(".config"));

        Assert.Equal(["notes.txt"], Names(listing));

        listing.ShowHidden = true;

        Assert.Equal([".bashrc", ".config", "notes.txt"], Names(listing));

        listing.ShowHidden = false;

        Assert.Equal(["notes.txt"], Names(listing));
    }

    [Fact]
    public void TheSystemsOwnFilesStayOutEitherWay()
    {
        DirectoryListing listing = Listing(Item("notes.txt"), SystemItem("pagefile.sys"));

        Assert.Equal(["notes.txt"], Names(listing));

        listing.ShowHidden = true;

        Assert.Equal(["notes.txt"], Names(listing));
    }

    [Fact]
    public void ASelectionFilterHidesNonMatchingFilesButKeepsFolders()
    {
        DirectoryListing listing = Listing(
            Item("photo.png"), Item("notes.txt"), Item("Documents", isDirectory: true));

        listing.SetSelectionFilter(["*.png"]);

        Assert.Equal(["Documents", "photo.png"], Names(listing).Order());

        listing.SetSelectionFilter(null);

        Assert.Equal(["Documents", "notes.txt", "photo.png"], Names(listing).Order());
    }

    [Fact]
    public void AFilterOnlySeesWhatTheListIsShowing()
    {
        DirectoryListing listing = Listing(Item("config.json"), Hidden(".config"));
        listing.InLocalSearch = true;
        listing.SearchCurrentDirectoryText = "config";

        Assert.Equal(["config.json"], Names(listing));

        listing.ShowHidden = true;

        Assert.Equal([".config", "config.json"], Names(listing).Order());
    }

    [Fact]
    public void AHiddenItemArrivingFromTheWatcherFollowsTheSameRule()
    {
        DirectoryListing listing = Listing(Item("notes.txt"));

        listing.Upsert(Hidden(".hidden.txt"));

        Assert.Equal(["notes.txt"], Names(listing));

        listing.ShowHidden = true;

        Assert.Equal([".hidden.txt", "notes.txt"], Names(listing));
    }

    [Fact]
    public void RenamingSomethingToADotNameTakesItOutOfTheList()
    {
        DirectoryListing listing = Listing(Item("notes.txt"));

        listing.Rename(At("notes.txt"), Hidden(".notes.txt"));

        Assert.Empty(Names(listing));

        listing.ShowHidden = true;

        Assert.Equal([".notes.txt"], Names(listing));
    }

    [Fact]
    public void FoldersComeFirstThenNamesIgnoringCase()
    {
        DirectoryListing listing = Listing(
            Item("zebra.txt"), Item("Apple.txt"), Item("src", isDirectory: true), Item("bin", isDirectory: true));

        Assert.Equal(["bin", "src", "Apple.txt", "zebra.txt"], Names(listing));
    }

    [Fact]
    public void ABigFolderStillLandsInTheRightOrder()
    {
        FolderItem[] items = [.. Enumerable.Range(0, 500).Select(i => Item($"file{i:D4}.txt"))];
        DirectoryListing listing = Listing(items);

        Assert.Equal(500, listing.Items.Count);
        Assert.Equal("file0000.txt", listing.Items[0].Name);
        Assert.Equal("file0499.txt", listing.Items[^1].Name);
    }

    [Fact]
    public void FilteringNarrowsToTheMatches()
    {
        DirectoryListing listing = Listing(Item("notes.txt"), Item("report.pdf"), Item("nope.md"));
        listing.InLocalSearch = true;

        listing.SearchCurrentDirectoryText = "no";

        Assert.Equal(["nope.md", "notes.txt"], Names(listing).Order());
    }

    /// <summary>
    /// Each extra character filters what survived the last one instead of the
    /// whole folder; the answer has to be the same either way.
    /// </summary>
    [Fact]
    public void TypingOneCharacterAtATimeMatchesFilteringInOneGo()
    {
        FolderItem[] items =
        [
            Item("report-2024.txt"), Item("report-2025.txt"), Item("readme.md"),
            Item("recipes.txt"), Item("notes.txt"), Item("rope.txt"),
        ];

        DirectoryListing typed = Listing(items);
        typed.InLocalSearch = true;
        foreach (string query in new[] { "r", "re", "rep", "repo" })
            typed.SearchCurrentDirectoryText = query;

        DirectoryListing atOnce = Listing(items);
        atOnce.InLocalSearch = true;
        atOnce.SearchCurrentDirectoryText = "repo";

        Assert.Equal(Names(atOnce), Names(typed));
        Assert.Equal(["report-2024.txt", "report-2025.txt"], Names(typed));
    }

    [Fact]
    public void BackspacingWidensTheFilterAgain()
    {
        DirectoryListing listing = Listing(Item("alpha.txt"), Item("album.txt"), Item("beta.txt"));
        listing.InLocalSearch = true;

        listing.SearchCurrentDirectoryText = "alp";
        Assert.Equal(["alpha.txt"], Names(listing));

        listing.SearchCurrentDirectoryText = "al";
        Assert.Equal(["album.txt", "alpha.txt"], Names(listing));
    }

    [Fact]
    public void LeavingTheSearchBoxKeepsTheFilterUntilTheQueryIsCleared()
    {
        DirectoryListing listing = Listing(Item("a.txt"), Item("b.txt"));
        listing.InLocalSearch = true;
        listing.SearchCurrentDirectoryText = "a";
        Assert.Single(listing.Items);

        // Leaving the box (InLocalSearch off) is not the same as clearing
        // the query — the filtered view stays up.
        listing.InLocalSearch = false;
        listing.ApplyView();
        Assert.Single(listing.Items);

        listing.SearchCurrentDirectoryText = "";
        Assert.Equal(2, listing.Items.Count);
    }

    [Fact]
    public void AnItemAddedWhileFilteringShowsUpIfItMatches()
    {
        DirectoryListing listing = Listing(Item("alpha.txt"));
        listing.InLocalSearch = true;
        listing.SearchCurrentDirectoryText = "al";

        listing.Upsert(Item("album.txt"));

        Assert.Equal(["album.txt", "alpha.txt"], Names(listing));
    }

    [Fact]
    public void ARenameWhileFilteringIsRecheckedAgainstTheFilter()
    {
        DirectoryListing listing = Listing(Item("alpha.txt"), Item("beta.txt"));
        listing.InLocalSearch = true;
        listing.SearchCurrentDirectoryText = "al";
        Assert.Equal(["alpha.txt"], Names(listing));

        listing.Rename(At("beta.txt"), Item("also.txt"));

        Assert.Equal(["alpha.txt", "also.txt"], Names(listing));
    }

    [Fact]
    public void RemovingAnItemTakesItOutOfTheFilteredView()
    {
        DirectoryListing listing = Listing(Item("alpha.txt"), Item("album.txt"));
        listing.InLocalSearch = true;
        listing.SearchCurrentDirectoryText = "al";

        listing.RemoveWithApply(At("album.txt"));

        Assert.Equal(["alpha.txt"], Names(listing));
    }

    [Fact]
    public void HiddenAndSystemItemsAreNotShown()
    {
        DirectoryListing listing = new(new NullIconCache());
        listing.Load(listing.CurrentDir, [
            Item("visible.txt"),
            new("hidden.txt", At("hidden.txt"), FileAttributes.Hidden, DateTime.UnixEpoch, false, 1, ".txt"),
            new("sys.txt", At("sys.txt"), FileAttributes.System, DateTime.UnixEpoch, false, 1, ".txt"),
        ]);

        Assert.Equal(["visible.txt"], Names(listing));
    }

    [Fact]
    public void TheHighlightStaysOnTheSameItemWhenTheListIsRebuilt()
    {
        DirectoryListing listing = Listing(Item("a.txt"), Item("b.txt"), Item("c.txt"));
        listing.ListSelection.SelectPath(At("b.txt"));

        listing.Upsert(Item("aa.txt"));

        Assert.Equal("b.txt", listing.ListSelection.SelectedItem!.Name);
    }

    [Fact]
    public void EmptyDirectoryFlagFollowsWhatIsVisible()
    {
        DirectoryListing listing = Listing(Item("a.txt"));
        Assert.False(listing.EmptyDirectory);

        listing.InLocalSearch = true;
        listing.SearchCurrentDirectoryText = "zzz";

        Assert.True(listing.EmptyDirectory);
    }

    [Fact]
    public void AWatcherEventFromAnotherFolderIsNotAddedToThisOne()
    {
        DirectoryListing listing = Listing(Item("notes.txt"));

        listing.Upsert(new FolderItem(
            "stray.txt", Path.Combine(_root, "sub", "stray.txt"),
            FileAttributes.Normal, DateTime.UnixEpoch, false, 1, ".txt"));

        Assert.Equal(["notes.txt"], Names(listing));
    }

    [Fact]
    public void SomethingRenamedOutOfThisFolderLeavesTheList()
    {
        DirectoryListing listing = Listing(Item("notes.txt"), Item("keep.txt"));

        listing.Rename(At("notes.txt"), new FolderItem(
            "notes.txt", Path.Combine(_root, "sub", "notes.txt"),
            FileAttributes.Normal, DateTime.UnixEpoch, false, 1, ".txt"));

        Assert.Equal(["keep.txt"], Names(listing));
    }

    [Fact]
    public void TheBoxesAtTheTopFollowTheFolderYouAreIn()
    {
        DirectoryListing listing = Listing();
        List<string> announced = [];
        listing.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DirectoryListing.Crumbs))
                announced.Add(listing.Crumbs[^1].Label);
        };

        listing.CurrentDir = Path.Combine(_root, "sub", "deeper");

        Assert.Equal(["deeper"], announced);
        Assert.Equal("sub", listing.Crumbs[^2].Label);
    }

    [Fact]
    public void ABatchOfWatcherEventsLandsCorrectlyInOnePass()
    {
        DirectoryListing listing = Listing(Item("keep.txt"), Item("old.txt"), Item("rename-me.txt"));

        listing.ApplyBatch([
            new WatchEvent.Upserted(Item("new.txt")),
            new WatchEvent.Deleted(At("old.txt")),
            new WatchEvent.Renamed(At("rename-me.txt"), Item("renamed.txt")),
        ]);

        Assert.Equal(["keep.txt", "new.txt", "renamed.txt"], Names(listing).Order());
    }

    /// <summary>
    /// Guards the fix for a folder that chokes when it changes fast: a big
    /// batch has to cost one resort and one rebuild, not one apiece, so this
    /// has to stay fast even as the batch grows.
    /// </summary>
    [Fact]
    public void ABigBatchOfWatcherEventsStaysFast()
    {
        FolderItem[] existing = [.. Enumerable.Range(0, 2_000).Select(i => Item($"old{i:D5}.txt"))];
        DirectoryListing listing = Listing(existing);

        WatchEvent[] batch =
            [.. Enumerable.Range(0, 3_000).Select(i => new WatchEvent.Upserted(Item($"new{i:D5}.txt")))];

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        listing.ApplyBatch(batch);
        sw.Stop();

        Assert.Equal(5_000, listing.Items.Count);
        Assert.True(sw.ElapsedMilliseconds < 2_000,
            $"applying a 3,000-event batch took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void DefaultsToNameOrderUntilToldOtherwise()
    {
        DirectoryListing listing = Listing(Item("b.txt"), Item("a.txt"), Item("sub", isDirectory: true));

        Assert.Equal(["sub", "a.txt", "b.txt"], Names(listing));
    }

    [Fact]
    public void SortBySizeOrdersSmallestFirstThenDescendingOnASecondPress()
    {
        DirectoryListing listing = Listing(
            Item("small.txt") with { Size = 10 },
            Item("large.txt") with { Size = 1000 },
            Item("medium.txt") with { Size = 100 });

        listing.SetSort(SortKey.Size);
        Assert.Equal(["large.txt", "medium.txt", "small.txt"], Names(listing));

        listing.SetSort(SortKey.Size);
        Assert.Equal(["small.txt", "medium.txt", "large.txt"], Names(listing));
    }

    [Fact]
    public void SortByModifiedOrdersNewestFirstByDefault()
    {
        DateTime now = DateTime.UtcNow;
        DirectoryListing listing = Listing(
            Item("old.txt") with { LastWriteTime = now.AddDays(-2) },
            Item("new.txt") with { LastWriteTime = now },
            Item("mid.txt") with { LastWriteTime = now.AddDays(-1) });

        listing.SetSort(SortKey.Modified);

        Assert.Equal(["new.txt", "mid.txt", "old.txt"], Names(listing));
    }

    [Fact]
    public void DirectoriesStayFirstRegardlessOfSortKey()
    {
        DirectoryListing listing = Listing(
            Item("z-file.txt") with { Size = 5 },
            Item("a-folder", isDirectory: true));

        listing.SetSort(SortKey.Size);

        Assert.Equal(["a-folder", "z-file.txt"], Names(listing));
    }

    [Fact]
    public void NavigatingIntoDownloadsDefaultsToNewestFirst()
    {
        string downloads = RovePaths.DownloadsDirectory;
        DirectoryListing listing = new(new NullIconCache());
        DateTime now = DateTime.UtcNow;

        listing.Load(downloads, [
            new("old.zip", Path.Combine(downloads, "old.zip"), FileAttributes.Normal, now.AddDays(-3), false, 1, ".zip"),
            new("new.zip", Path.Combine(downloads, "new.zip"), FileAttributes.Normal, now, false, 1, ".zip"),
        ]);

        Assert.Equal(SortKey.Modified, listing.SortBy);
        Assert.True(listing.SortDescending);
        Assert.Equal(["new.zip", "old.zip"], Names(listing));
    }

    [Fact]
    public void RevisitingDownloadsResetsAManualSortBackToNewestFirst()
    {
        string downloads = RovePaths.DownloadsDirectory;
        DirectoryListing listing = new(new NullIconCache()) { CurrentDir = downloads };
        listing.Load(downloads, [Item("a.zip"), Item("b.zip")]);
        listing.SetSort(SortKey.Name);

        // Real navigation, the way ContentViewModel drives it: Load the new
        // folder first, then move CurrentDir to match.
        listing.Load(_root, [Item("c.txt")]);
        listing.CurrentDir = _root;

        listing.Load(downloads, [Item("a.zip"), Item("b.zip")]);
        listing.CurrentDir = downloads;

        Assert.Equal(SortKey.Modified, listing.SortBy);
        Assert.True(listing.SortDescending);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;
using System.Collections.ObjectModel;

namespace Rove.UI.ViewModels;

public partial class BookmarksViewModel : ViewModelBase
{
    private readonly CommandRegistry _registry;
    private readonly BookmarkStore _store;

    public event Action<Bookmark>? GoRequested;

    public event Action<string>? InfoRaised;

    public BookmarksViewModel(CommandRegistry registry, BookmarkStore store)
    {
        _registry = registry;
        _store = store;
        _store.Changed += () =>
        {
            if (IsOpen)
                Rebuild();
        };
        RegisterBindings();
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private ObservableCollection<BookmarkRow> _items = [];

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _inAddBookmark;

    [ObservableProperty]
    private string _addBookmarkPath = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        if (IsOpen)
            Rebuild();
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        SearchText = string.Empty;
        IsOpen = true;
        Rebuild();
    }

    private void Close()
    {
        IsOpen = false;
        InAddBookmark = false;
        SearchText = string.Empty;
        Items = [];
        SelectedIndex = -1;
    }

    private void Rebuild()
    {
        BookmarkEntry[] entries =
        [
            .. _store.Items
                .Select((bookmark, index) => new BookmarkEntry(bookmark, BookmarkStore.ShortcutFor(index)))
                .Where(entry => Matches(entry.Bookmark))
        ];
        Items =
        [
            new BookmarkRow(IsAddNew: true, Entry: null),
            .. entries.Select(entry => new BookmarkRow(IsAddNew: false, Entry: entry)),
        ];
        IsEmpty = entries.Length == 0;
        ResetSelection();
    }

    private bool Matches(Bookmark bookmark) =>
        SearchText.Length == 0
        || FuzzyMatcher.TryMatch(SearchText, bookmark.Name, out _)
        || FuzzyMatcher.TryMatch(SearchText, bookmark.Path, out _);

    public void MoveUp()
    {
        if (Items.Count == 0)
            return;
        SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
    }

    public void MoveDown()
    {
        if (Items.Count == 0)
            return;
        SelectedIndex = SelectedIndex >= Items.Count - 1 ? 0 : SelectedIndex + 1;
    }

    public void GoToSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
            return;
        BookmarkRow row = Items[SelectedIndex];
        if (row.IsAddNew)
        {
            StartAddBookmark();
            return;
        }
        Bookmark bookmark = row.Entry!.Bookmark;
        Close();
        GoRequested?.Invoke(bookmark);
    }

    public void RemoveSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
            return;
        if (Items[SelectedIndex].Entry is not { } entry)
            return;
        if (_store.Remove(entry.Bookmark.Path))
            InfoRaised?.Invoke($"Removed the bookmark for {entry.Bookmark.Name}.");
    }

    private void StartAddBookmark()
    {
        AddBookmarkPath = string.Empty;
        InAddBookmark = true;
    }

    private void CancelAddBookmark() => InAddBookmark = false;

    private void ApplyAddBookmark()
    {
        string typed = AddBookmarkPath.Trim();
        if (typed.Length == 0)
        {
            InAddBookmark = false;
            return;
        }

        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (PathResolver.Resolve(typed, baseDir) is not { } path)
        {
            InfoRaised?.Invoke($"That is not a path: {typed}.");
            return;
        }

        string io = LongPath.ForIo(path);
        bool isDirectory = Directory.Exists(io);
        if (!isDirectory && !File.Exists(io))
        {
            InfoRaised?.Invoke($"There is nothing at {path}.");
            return;
        }

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(path)));
        if (name.Length == 0)
            name = LongPath.Display(path);

        InAddBookmark = false;
        bool added = _store.Toggle(new Bookmark(path, name, isDirectory));
        InfoRaised?.Invoke(added ? $"Bookmarked {name}." : $"Removed the bookmark for {name}.");
        Rebuild();
    }

    private void ResetSelection() => SelectedIndex = Items.Count > 1 ? 1 : 0;

    private void RegisterBindings()
    {
        _registry.Register(CommandDef.ShowBookmarks, Toggle);
        _registry.Register(CommandDef.BookmarkMoveUp, MoveUp);
        _registry.Register(CommandDef.BookmarkMoveDown, MoveDown);
        _registry.Register(CommandDef.BookmarkExecute, GoToSelected);
        _registry.Register(CommandDef.RemoveBookmark, RemoveSelected);
        _registry.Register(CommandDef.ApplyAddBookmark, ApplyAddBookmark);
        _registry.Register(CommandDef.CancelAddBookmark, CancelAddBookmark);
    }
}

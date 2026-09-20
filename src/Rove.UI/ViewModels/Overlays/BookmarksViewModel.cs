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
    private ObservableCollection<BookmarkEntry> _items = [];

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public bool IsEmpty => Items.Count == 0;

    partial void OnItemsChanged(ObservableCollection<BookmarkEntry> value) => OnPropertyChanged(nameof(IsEmpty));

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
        SearchText = string.Empty;
        Items = [];
        SelectedIndex = -1;
    }

    private void Rebuild()
    {
        Items =
        [
            .. _store.Items
                .Select((bookmark, index) => new BookmarkEntry(bookmark, BookmarkStore.ShortcutFor(index)))
                .Where(entry => Matches(entry.Bookmark))
        ];
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
        Bookmark bookmark = Items[SelectedIndex].Bookmark;
        Close();
        GoRequested?.Invoke(bookmark);
    }

    public void RemoveSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
            return;
        Bookmark bookmark = Items[SelectedIndex].Bookmark;
        if (_store.Remove(bookmark.Path))
            InfoRaised?.Invoke($"Removed the bookmark for {bookmark.Name}.");
    }

    private void ResetSelection()
    {
        SelectedIndex = -1;
        if (Items.Count > 0)
            SelectedIndex = 0;
    }

    private void RegisterBindings()
    {
        _registry.Register(CommandDef.ShowBookmarks, Toggle);
        _registry.Register(CommandDef.BookmarkMoveUp, MoveUp);
        _registry.Register(CommandDef.BookmarkMoveDown, MoveDown);
        _registry.Register(CommandDef.BookmarkExecute, GoToSelected);
        _registry.Register(CommandDef.RemoveBookmark, RemoveSelected);
    }
}

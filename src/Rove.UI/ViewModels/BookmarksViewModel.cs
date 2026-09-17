using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Rove.UI.ViewModels;

/// <summary>One row of the bookmark list: what to show, and the key it answers to.</summary>
public record BookmarkEntry(Bookmark Bookmark, string Shortcut);

/// <summary>
/// The bookmark list, which only exists while it is on screen. It works the
/// way the palette works — type to narrow, Ctrl+N/Ctrl+P to move, Enter to
/// go — because it is the same gesture pointed at places instead of verbs.
/// </summary>
public partial class BookmarksViewModel : ViewModelBase
{
    private readonly CommandRegistry _registry;
    private readonly BookmarkStore _store;

    /// <summary>Somewhere to go: a folder to open, or a file to land on.</summary>
    public event Action<Bookmark>? GoRequested;

    /// <summary>Neutral feedback ("Removed the bookmark for src").</summary>
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

    /// <summary>Shown in place of the list when there is nothing bookmarked yet.</summary>
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

    /// <summary>
    /// The list, narrowed by whatever has been typed. A bookmark keeps the
    /// shortcut its position in the whole list earns it, so filtering never
    /// moves a key out from under the user.
    /// </summary>
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

    /// <summary>Takes the highlighted bookmark out of the list, leaving the list open.</summary>
    public void RemoveSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
            return;
        Bookmark bookmark = Items[SelectedIndex].Bookmark;
        if (_store.Remove(bookmark.Path))
            InfoRaised?.Invoke($"Removed the bookmark for {bookmark.Name}.");
    }

    /// <summary>
    /// After ItemsSource is swapped the ListBox resets its own selection;
    /// bounce through -1 so setting 0 always re-notifies the binding.
    /// </summary>
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

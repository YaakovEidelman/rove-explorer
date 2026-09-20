using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rove.Core;
using Rove.UI.Services;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Rove.UI.ViewModels;

/// <summary>
/// The tabs, and which one is in front. Everything that used to talk to the
/// one folder view now talks to this, and it passes the message on to the tab
/// showing — so the window, the palette, the bookmarks and the search all stay
/// ignorant of how many tabs there are.
///
/// <para>
/// A tab is built here rather than handed in, because opening one is something
/// the user does at any moment and something has to know how. That is why this
/// holds the pieces every folder view needs: they are shared by all of them,
/// and only the view itself is per tab.
/// </para>
/// </summary>
public partial class TabsViewModel : ViewModelBase, IDisposable
{
    private readonly CommandRegistry _registry;
    private readonly RoveCore _core;
    private readonly FileClipboard _clipboard;
    private readonly IRoveClipboardService _systemClipboard;
    private readonly IIconCache _icons;
    private readonly FileOperationViewModel _operation;
    private readonly BookmarkStore _bookmarks;
    private readonly SettingsStore _settings;

    /// <summary>One undo history for the app, not one per tab.</summary>
    private readonly UndoStack _undo = new();

    /// <summary>Something the tab in front wants said in the status bar.</summary>
    public event Action<string>? ErrorRaised;

    /// <summary>Neutral feedback from the tab in front.</summary>
    public event Action<string>? InfoRaised;

    /// <summary>The tab in front wants a destructive verb confirmed.</summary>
    public event Action<string, Action>? ConfirmRequested;

    /// <summary>The tab in front wants the drive list put up.</summary>
    public event Action? DrivePickerRequested;

    public event Action<Task>? AppPickerRequested;

    /// <summary>Anything changed that the status bar reads off the tab in front.</summary>
    public event Action? SurfaceChanged;

    /// <summary>The highlight moved in the tab in front.</summary>
    public event Action? SelectionChanged;

    /// <summary>A different tab came to the front.</summary>
    public event Action? ActiveChanged;

    /// <summary>
    /// The last tab was closed. Closing the last one closes Rove — a window
    /// with no folder in it is nothing anyone wants to look at — but shutting
    /// down belongs to the window, not here.
    /// </summary>
    public event Action? Emptied;

    public ObservableCollection<FolderTab> Items { get; } = [];

    public TabsViewModel(
        CommandRegistry registry,
        RoveCore core,
        FileClipboard clipboard,
        IRoveClipboardService systemClipboard,
        IIconCache icons,
        FileOperationViewModel operation,
        BookmarkStore bookmarks,
        SettingsStore settings
    )
    {
        _registry = registry;
        _core = core;
        _clipboard = clipboard;
        _systemClipboard = systemClipboard;
        _icons = icons;
        _operation = operation;
        _bookmarks = bookmarks;
        _settings = settings;

        Items.CollectionChanged += OnTabsChanged;
        RegisterBindings();
    }

    [ObservableProperty]
    private int _activeIndex = -1;

    /// <summary>The folder view showing. Once open, there is always exactly one.</summary>
    public ContentViewModel Active => Items[ActiveIndex].Content;

    /// <summary>Shown once there is a tab to show, even if it is the only one.</summary>
    public bool IsStripVisible => Items.Count > 0;

    partial void OnActiveIndexChanged(int value) => Activate();

    /// <summary>
    /// Points everything at the tab that is now in front. Called on its own
    /// after a close as well, where the index can land on a different tab
    /// without the number itself changing.
    /// </summary>
    private void Activate()
    {
        for (int i = 0; i < Items.Count; i++)
            Items[i].IsActive = i == ActiveIndex;

        OnPropertyChanged(nameof(Active));
        ActiveChanged?.Invoke();
        SelectionChanged?.Invoke();
        SurfaceChanged?.Invoke();
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsStripVisible));
        SurfaceChanged?.Invoke();
    }

    // ── opening and closing ──────────────────────────────────────────────

    /// <summary>
    /// Opens a tab on <paramref name="directory"/> and brings it to the
    /// front. The first tab is opened this way too, so there is no second
    /// path by which a tab can come into being.
    ///
    /// <para>
    /// Setting the folder is all it takes to load it: a new view queues its
    /// first listing behind whatever is on the UI thread, and reads the
    /// folder named by the time that runs.
    /// </para>
    /// </summary>
    public FolderTab Open(string directory)
    {
        TabCommands commands = NewCommands();
        FolderTab tab = new(NewContent(commands), commands, Select, CloseTabItem);
        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FolderTab.IsRenaming))
                SurfaceChanged?.Invoke();
        };
        tab.Content.DirectoryListing.CurrentDir = directory;
        Items.Add(tab);
        ActiveIndex = Items.Count - 1;
        return tab;
    }

    /// <summary>A tab on the same folder as the one in front — also the strip's + button.</summary>
    [RelayCommand]
    private void NewTab() => Open(Active.DirectoryListing.CurrentDir);

    /// <summary>
    /// A tab on the highlighted folder. With a file highlighted there is no
    /// folder to open, so this opens the one it is sitting in.
    /// </summary>
    private void OpenInNewTab() =>
        Open(Active.HighlightedItem is { Item.IsDirectory: true } highlighted
            ? highlighted.Item.FullPath
            : Active.DirectoryListing.CurrentDir);

    /// <summary>
    /// Closes the tab in front. The last one closing means Rove is closing:
    /// what would be left otherwise is a window with nothing in it.
    /// </summary>
    private void CloseTab() => CloseTabItem(Items[ActiveIndex]);

    /// <summary>Brings a clicked-on tab to the front.</summary>
    private void Select(FolderTab tab)
    {
        int index = Items.IndexOf(tab);
        if (index >= 0)
            ActiveIndex = index;
    }

    /// <summary>
    /// Closes whichever tab its x was clicked on — not necessarily the one in
    /// front. The last one closing means Rove is closing: what would be left
    /// otherwise is a window with nothing in it.
    /// </summary>
    private void CloseTabItem(FolderTab tab)
    {
        int closing = Items.IndexOf(tab);
        if (closing < 0)
            return;

        if (Items.Count <= 1)
        {
            Emptied?.Invoke();
            return;
        }

        bool wasActive = closing == ActiveIndex;
        Items.RemoveAt(closing);
        tab.Dispose();

        if (wasActive)
        {
            // Whatever took its place, or the last tab when it was at the end.
            int next = Math.Min(closing, Items.Count - 1);
            if (next == ActiveIndex)
                Activate(); // same number, different tab
            else
                ActiveIndex = next;
        }
        else if (closing < ActiveIndex)
        {
            ActiveIndex--; // the tab in front shifted down one slot
        }
    }

    private void NextTab() => Step(1);

    private void PreviousTab() => Step(-1);

    private void Step(int direction)
    {
        if (Items.Count > 1)
            ActiveIndex = (ActiveIndex + direction + Items.Count) % Items.Count;
    }

    /// <summary>Goes to the nth tab, and does nothing when there is no nth tab.</summary>
    private void GoTo(int index)
    {
        if (index >= 0 && index < Items.Count)
            ActiveIndex = index;
    }

    private void ToggleRenameTab()
    {
        FolderTab? renaming = Items.FirstOrDefault(t => t.IsRenaming);
        if (renaming is not null)
            renaming.CancelRename();
        else if (ActiveIndex >= 0 && ActiveIndex < Items.Count)
            Items[ActiveIndex].BeginRename();
    }

    private void ApplyRenameTab() => Items.FirstOrDefault(t => t.IsRenaming)?.ApplyRename();

    // ── building one ─────────────────────────────────────────────────────

    private TabCommands NewCommands() => new(_registry, () => CommandsInFront);

    private TabCommands? CommandsInFront =>
        ActiveIndex >= 0 && ActiveIndex < Items.Count ? Items[ActiveIndex].Commands : null;

    /// <summary>
    /// A folder view, wired to say what it has to say only while it is the
    /// tab in front. Its commands go into <paramref name="commands"/>, which
    /// is what works out whose turn it is when a key arrives.
    /// </summary>
    private ContentViewModel NewContent(TabCommands commands)
    {
        ContentViewModel content = new(
            commands, _core, _clipboard, _systemClipboard, _icons, _operation, _bookmarks, _undo, _settings);

        content.ErrorRaised += message => FromFront(content, () => ErrorRaised?.Invoke(message));
        content.InfoRaised += message => FromFront(content, () => InfoRaised?.Invoke(message));
        content.ConfirmRequested += (message, act) =>
            FromFront(content, () => ConfirmRequested?.Invoke(message, act));
        content.DrivePickerRequested += () => FromFront(content, () => DrivePickerRequested?.Invoke());
        content.AppPickerRequested += loading => FromFront(content, () => AppPickerRequested?.Invoke(loading));

        content.PropertyChanged += (_, _) => FromFront(content, RaiseSurface);
        content.DirectoryListing.PropertyChanged += (_, _) => FromFront(content, RaiseSurface);
        content.Completions.PropertyChanged += (_, _) => FromFront(content, RaiseSurface);
        content.DirectoryListing.Items.CollectionChanged += (_, _) => FromFront(content, RaiseSurface);
        content.DirectoryListing.ListSelection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ListSelection.Index))
                return;
            FromFront(content, () =>
            {
                SelectionChanged?.Invoke();
                RaiseSurface();
            });
        };
        return content;
    }

    /// <summary>
    /// Passes something on only when it came from the tab in front. A tab
    /// behind can still be reading a folder or finishing a copy, and its news
    /// does not belong in a status bar describing a different folder.
    /// </summary>
    private void FromFront(ContentViewModel source, Action raise)
    {
        if (ActiveIndex >= 0 && ActiveIndex < Items.Count
            && ReferenceEquals(Items[ActiveIndex].Content, source))
        {
            raise();
        }
    }

    private void RaiseSurface() => SurfaceChanged?.Invoke();

    // ── keys ─────────────────────────────────────────────────────────────

    private void RegisterBindings()
    {
        _registry.Register(CommandDef.NewTab, NewTab);
        _registry.Register(CommandDef.OpenInNewTab, OpenInNewTab);
        _registry.Register(CommandDef.CloseTab, CloseTab);
        _registry.Register(CommandDef.NextTab, NextTab);
        _registry.Register(CommandDef.PreviousTab, PreviousTab);
        _registry.Register(CommandDef.ToggleRenameTab, ToggleRenameTab);
        _registry.Register(CommandDef.ApplyRenameTab, ApplyRenameTab);

        // One command per slot rather than per tab: the slots are fixed, and
        // which tab a slot leads to is a question asked when the key is hit.
        for (int slot = 0; slot < CommandDef.TabShortcutCount; slot++)
        {
            int index = slot;
            _registry.Register(CommandDef.TabGo(index), () => GoTo(index));
        }
    }

    public void Dispose()
    {
        foreach (FolderTab tab in Items)
            tab.Dispose();
        Items.Clear();
    }
}

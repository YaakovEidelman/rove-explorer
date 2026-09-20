using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rove.Core;
using Rove.UI.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Rove.UI.ViewModels;

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

    private readonly UndoStack _undo = new();

    public event Action<string>? ErrorRaised;

    public event Action<string>? InfoRaised;

    public event Action<string, Action>? ConfirmRequested;

    public event Action? DrivePickerRequested;

    public event Action<Task>? AppPickerRequested;

    public event Action? SurfaceChanged;

    public event Action? SelectionChanged;

    public event Action? ActiveChanged;

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

    public ContentViewModel Active => Items[ActiveIndex].Content;

    public bool IsStripVisible => Items.Count > 0;

    partial void OnActiveIndexChanged(int value) => Activate();

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

    [RelayCommand]
    private void NewTab() => Open(Active.DirectoryListing.CurrentDir);

    private void OpenInNewTab() =>
        Open(Active.HighlightedItem is { Item.IsDirectory: true } highlighted
            ? highlighted.Item.FullPath
            : Active.DirectoryListing.CurrentDir);

    private void CloseTab() => CloseTabItem(Items[ActiveIndex]);

    private void Select(FolderTab tab)
    {
        int index = Items.IndexOf(tab);
        if (index >= 0)
            ActiveIndex = index;
    }

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
            int next = Math.Min(closing, Items.Count - 1);
            if (next == ActiveIndex)
                Activate();
            else
                ActiveIndex = next;
        }
        else if (closing < ActiveIndex)
        {
            ActiveIndex--;
        }
    }

    private void NextTab() => Step(1);

    private void PreviousTab() => Step(-1);

    private void Step(int direction)
    {
        if (Items.Count > 1)
            ActiveIndex = (ActiveIndex + direction + Items.Count) % Items.Count;
    }

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

    private TabCommands NewCommands() => new(_registry, () => CommandsInFront);

    private TabCommands? CommandsInFront =>
        ActiveIndex >= 0 && ActiveIndex < Items.Count ? Items[ActiveIndex].Commands : null;

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

    private void FromFront(ContentViewModel source, Action raise)
    {
        if (ActiveIndex >= 0 && ActiveIndex < Items.Count
            && ReferenceEquals(Items[ActiveIndex].Content, source))
        {
            raise();
        }
    }

    private void RaiseSurface() => SurfaceChanged?.Invoke();

    private void RegisterBindings()
    {
        _registry.Register(CommandDef.NewTab, NewTab);
        _registry.Register(CommandDef.OpenInNewTab, OpenInNewTab);
        _registry.Register(CommandDef.CloseTab, CloseTab);
        _registry.Register(CommandDef.NextTab, NextTab);
        _registry.Register(CommandDef.PreviousTab, PreviousTab);
        _registry.Register(CommandDef.ToggleRenameTab, ToggleRenameTab);
        _registry.Register(CommandDef.ApplyRenameTab, ApplyRenameTab);

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

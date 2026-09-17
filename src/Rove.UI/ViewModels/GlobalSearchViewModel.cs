using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rove.UI.ViewModels;

public partial class SearchResultItem : ObservableObject
{
    public FolderItem Item { get; }
    public string Name => Item.Name;
    public string Location { get; }

    [ObservableProperty]
    private IImage? _icon;

    public SearchResultItem(FolderItem item, string root, IIconCache cache)
    {
        Item = item;
        string? dir = Path.GetDirectoryName(item.FullPath);
        Location = dir is not null && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? "." + dir[root.Length..].TrimEnd(Path.DirectorySeparatorChar)
            : dir ?? "";
        _ = LoadIconAsync(cache);
    }

    private async Task LoadIconAsync(IIconCache cache)
    {
        Icon = await cache.GetIconAsync(Item, 16);
    }
}

/// <summary>
/// SEARCH_GLOBAL surface: deep fuzzy search from the current directory
/// downwards. Typing re-searches (debounced, cancelling the previous run);
/// Enter jumps to the selected hit in its folder.
/// </summary>
public partial class GlobalSearchViewModel : ViewModelBase
{
    private const int DebounceMs = 250;
    private const int MaxResults = 80;

    private readonly RoveCore _core;
    private readonly IIconCache _cache;
    private readonly Func<string> _rootProvider;
    private CancellationTokenSource? _cts;

    /// <summary>Asks the content view to navigate to (directory, highlightPath).</summary>
    public event Action<string, string?>? NavigateRequested;

    public event Action<string>? ErrorRaised;

    public GlobalSearchViewModel(CommandRegistry registry, RoveCore core, IIconCache cache, Func<string> rootProvider)
    {
        _core = core;
        _cache = cache;
        _rootProvider = rootProvider;

        registry.Register(CommandDef.ToggleGlobalSearch, Toggle);
        registry.Register(CommandDef.GlobalSearchMoveUp, MoveUp);
        registry.Register(CommandDef.GlobalSearchMoveDown, MoveDown);
        registry.Register(CommandDef.GlobalSearchExecute, ExecuteSelected);
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _root = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private ObservableCollection<SearchResultItem> _results = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    public void Toggle()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            Root = _rootProvider();
            Query = string.Empty;
            Results = [];
            SelectedIndex = -1;
        }
        else
        {
            _cts?.Cancel();
        }
    }

    partial void OnQueryChanged(string value) => _ = SearchAsync(value);

    private async Task SearchAsync(string query)
    {
        _cts?.Cancel();
        CancellationTokenSource cts = new();
        _cts = cts;

        if (string.IsNullOrWhiteSpace(query))
        {
            Results = [];
            SelectedIndex = -1;
            IsSearching = false;
            return;
        }

        try
        {
            await Task.Delay(DebounceMs, cts.Token);
            IsSearching = true;
            CommandResult<SearchHit[]> result =
                await _core.Search.SearchAsync(Root, query, MaxResults, cts.Token);

            if (cts.Token.IsCancellationRequested)
                return;

            if (result.Data is { } hits)
            {
                Results = [.. hits.Select(h => new SearchResultItem(h.Item, Root, _cache))];
                SelectedIndex = Results.Count > 0 ? 0 : -1;
            }
            if (!result.IsOk && result.Reason != "truncated")
                ErrorRaised?.Invoke(result.Message ?? "Search failed.");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                IsSearching = false;
        }
    }

    public void MoveUp()
    {
        if (Results.Count == 0)
            return;
        SelectedIndex = SelectedIndex <= 0 ? Results.Count - 1 : SelectedIndex - 1;
    }

    public void MoveDown()
    {
        if (Results.Count == 0)
            return;
        SelectedIndex = SelectedIndex >= Results.Count - 1 ? 0 : SelectedIndex + 1;
    }

    public void ExecuteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Results.Count)
            return;
        FolderItem item = Results[SelectedIndex].Item;
        Toggle();

        if (item.IsDirectory)
        {
            NavigateRequested?.Invoke(item.FullPath, null);
            return;
        }
        string? dir = Path.GetDirectoryName(item.FullPath);
        if (dir is not null)
            NavigateRequested?.Invoke(dir, item.FullPath);
    }
}

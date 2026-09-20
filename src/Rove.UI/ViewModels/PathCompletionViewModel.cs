using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core;
using Rove.Core.Protocol;
using Rove.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Rove.UI.ViewModels;

public record PathCompletionEntry(string Name, string FullPath, bool IsDirectory)
{
    public string Display => IsDirectory ? FullPath + Path.DirectorySeparatorChar : FullPath;
}

public partial class PathCompletionViewModel : ViewModelBase
{
    private readonly RoveCore _core;

    private int _generation;

    private string _typedBase = string.Empty;

    public PathCompletionViewModel(RoveCore core) => _core = core;

    public event Action<string>? Filled;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private ObservableCollection<PathCompletionEntry> _items = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _isReading;

    private int _reading;

    public async Task<string?> ExpandAsync(string typed, string currentDirectory)
    {
        if (await MatchesFor(typed, currentDirectory) is not { } matches)
            return null;

        if (matches.Count == 0)
        {
            Close();
            return null;
        }

        if (matches.Count == 1)
        {
            Close();
            return PathCompletion.Join(typed, matches[0].Name, matches[0].IsDirectory);
        }

        Show(matches, typed);
        string shared = PathCompletion.LongestCommonPrefix([.. matches.Select(match => match.Name)]);
        string prefix = PathCompletion.Split(typed, currentDirectory).Prefix;
        return shared.Length > prefix.Length
            ? PathCompletion.Join(typed, shared, isDirectory: false)
            : null;
    }

    public async Task NarrowAsync(string typed, string currentDirectory)
    {
        if (!IsOpen)
            return;

        if (await MatchesFor(typed, currentDirectory) is not { } matches)
            return;

        if (matches.Count == 0)
            Close();
        else
            Show(matches, typed);
    }

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

    partial void OnSelectedIndexChanged(int value)
    {
        if (value < 0 || value >= Items.Count)
            return;

        PathCompletionEntry chosen = Items[value];
        Filled?.Invoke(PathCompletion.Join(_typedBase, chosen.Name, chosen.IsDirectory));
    }

    public void Close()
    {
        IsOpen = false;
        Items = [];
        SelectedIndex = -1;
    }

    private async Task<IReadOnlyList<PathCompletionEntry>?> MatchesFor(string typed, string currentDirectory)
    {
        int mine = ++_generation;
        PathFragment fragment = PathCompletion.Split(typed, currentDirectory);
        if (fragment.Directory.Length == 0)
            return [];

        _reading++;
        IsReading = true;
        CommandResult<FolderItem[]> listing;
        try
        {
            listing = await _core.Actions.ReadDirectoryAsync(new(fragment.Directory));
        }
        finally
        {
            IsReading = --_reading > 0;
        }

        if (mine != _generation)
            return null;
        if (!listing.IsOk || listing.Data is null)
            return [];

        return
        [
            .. listing.Data
                .Where(item => IsOffered(item, fragment.Prefix))
                .OrderBy(item => !item.IsDirectory)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new PathCompletionEntry(item.Name, item.FullPath, item.IsDirectory))
        ];
    }

    private static bool IsOffered(FolderItem item, string prefix)
    {
        if (item.Attributes.HasFlag(FileAttributes.System))
            return false;
        if (prefix.Length == 0 && item.Attributes.HasFlag(FileAttributes.Hidden))
            return false;
        return PathCompletion.Matches(item.Name, prefix);
    }

    private void Show(IReadOnlyList<PathCompletionEntry> matches, string typed)
    {
        _typedBase = typed;
        Items = [.. matches];
        IsOpen = true;
        SelectedIndex = -1;
    }
}

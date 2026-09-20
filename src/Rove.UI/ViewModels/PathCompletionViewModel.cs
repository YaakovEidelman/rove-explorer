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

/// <summary>
/// One name the path bar could be finished with. The row shows the whole
/// path rather than the bare name, the way an address bar's list does, so a
/// glance tells you where the thing being offered actually is.
/// </summary>
public record PathCompletionEntry(string Name, string FullPath, bool IsDirectory)
{
    public string Display => IsDirectory ? FullPath + Path.DirectorySeparatorChar : FullPath;
}

/// <summary>
/// Tab completion for the path bar, and the list it puts up. It works the way
/// a shell works — Tab carries the text as far as the matches agree, and when
/// more than one is left the list appears to pick from with Ctrl+N/Ctrl+P.
/// Moving onto a name fills it into the path bar, so what the bar holds is
/// always where Enter goes.
///
/// <para>
/// It never touches the path bar's text itself: every method hands back the
/// text the bar should now hold, or raises <see cref="Filled"/> with it, and
/// <see cref="ContentViewModel"/> puts it there. One owner for the text means
/// the two can't disagree about it.
/// </para>
/// </summary>
public partial class PathCompletionViewModel : ViewModelBase
{
    private readonly RoveCore _core;

    /// <summary>
    /// Bumped on every request. A folder that is slow to read comes back
    /// after the user has typed more, and an answer to the older question is
    /// worse than no answer, so a stale one is dropped.
    /// </summary>
    private int _generation;

    /// <summary>What was typed when the list was last put up, which every fill is built from.</summary>
    private string _typedBase = string.Empty;

    public PathCompletionViewModel(RoveCore core) => _core = core;

    public event Action<string>? Filled;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private ObservableCollection<PathCompletionEntry> _items = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>
    /// A folder is being read to answer a Tab. Set the same way the file list
    /// and the deep search set theirs, so anything waiting on the app to be
    /// idle can see this work too.
    /// </summary>
    [ObservableProperty]
    private bool _isReading;

    /// <summary>Reads still running, so the last one out turns the flag off.</summary>
    private int _reading;

    /// <summary>
    /// Tab. Finishes the name outright when only one thing matches, and
    /// otherwise carries the text as far as the matches agree and puts the
    /// list up. Returns what the path bar should now hold, or null to leave
    /// it exactly as it is — which is also the answer when a second Tab has
    /// already overtaken this one.
    /// </summary>
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

    /// <summary>Keeps an open list in step with what is still being typed.</summary>
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

    /// <summary>
    /// What the folder holds that could finish the name, folders first and
    /// then by name, the same order the file list itself uses. Null means the
    /// answer arrived too late to be worth anything.
    /// </summary>
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

    /// <summary>
    /// The OS's own items are never offered. Hidden ones are offered only
    /// once something has been typed, so a bare Tab shows the folder the way
    /// the file list shows it.
    /// </summary>
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

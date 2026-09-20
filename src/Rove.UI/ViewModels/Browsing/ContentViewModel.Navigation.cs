using CommunityToolkit.Mvvm.Input;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    public async Task SetCurrentDirectoryAsync(
        string directory, string? highlightPath = null, bool asAdmin = false)
    {
        string from = DirectoryListing.CurrentDir;
        IsLoading = true;
        CommandResult<FolderItem[]> result;
        bool viaAdmin;
        try
        {
            (result, viaAdmin) = await ReadForNavigationAsync(directory, asAdmin);
        }
        finally
        {
            IsLoading = false;
        }

        if (!result.IsOk || result.Data is null)
        {
            if (!viaAdmin && CanRetryAsAdministrator(directory, result))
            {
                await SetCurrentDirectoryAsync(directory, highlightPath, asAdmin: true);
                return;
            }
            ErrorRaised?.Invoke(result.Message ?? $"Could not open {directory}.");
            return;
        }

        IsAdminView = viaAdmin;
        DirectoryListing.InLocalSearch = false;
        DirectoryListing.SearchCurrentDirectoryText = string.Empty;
        DirectoryListing.Load(directory, result.Data);
        DirectoryListing.CurrentDir = directory;
        InArchive = ArchivePath.IsInside(directory);
        InTrash = TrashService.BrowsePath is { } trashRoot && PathGuard.IsSameOrDescendant(trashRoot, directory);
        RefreshCutFlags();

        if (!_navigatingHistory && !PathCompare.PathMatches(from, directory))
        {
            _backHistory.Add(from);
            _forwardHistory.Clear();
            NotifyHistory();
        }

        if (highlightPath is not null)
            DirectoryListing.ListSelection.SelectPath(highlightPath);
        else
            DirectoryListing.ListSelection.Select(0);

        try
        {
            if (InArchive || IsAdminView)
                _watcher.Pause();
            else
                _watcher.ChangePath(directory);
        }
        catch (Exception)
        {
        }
    }

    private async Task<(CommandResult<FolderItem[]> Result, bool ViaAdmin)> ReadForNavigationAsync(
        string directory, bool asAdmin)
    {
        IAdminSession? admin = _core.Admin;
        if (asAdmin && admin is not null)
            return (await admin.ReadDirectoryAsync(directory), true);

        CommandResult<FolderItem[]> result = await _core.Actions.ReadDirectoryAsync(new(directory));
        if (result.Reason == "permission_denied" && admin is { IsRunning: true })
            return (await admin.ReadDirectoryAsync(directory), true);
        return (result, false);
    }

    private bool CanRetryAsAdministrator(string directory, CommandResult<FolderItem[]> result) =>
        result.Reason == "permission_denied"
        && _core.Admin is not null
        && !ArchivePath.IsInside(directory);

    public Task ReloadCurrentDirectoryAsync() =>
        SetCurrentDirectoryAsync(DirectoryListing.CurrentDir, HighlightedItem?.Item.FullPath);

    public void GetItem()
    {
        DirectoryListing.FlushPendingFilter();
        if (HighlightedItem is not { } selected)
            return;
        FolderItem item = selected.Item;
        if (item.IsDirectory)
        {
            _ = SetCurrentDirectoryAsync(item.FullPath);
            return;
        }
        ClearLocalSearch();
        if (IsAdminView)
        {
            _ = OpenFromAdminAsync(item);
            return;
        }
        if (ArchiveService.IsArchive(item.FullPath))
        {
            _ = OpenArchiveAsync(item);
            return;
        }
        if (InArchive)
        {
            _ = OpenFromArchiveAsync(item);
            return;
        }
        _ = LaunchAsync(item);
    }

    private async Task OpenArchiveAsync(FolderItem item)
    {
        string path = item.FullPath;
        if (InArchive)
        {
            if (await CopyOutAsync(item) is not { } copy)
                return;
            path = copy;
        }
        await SetCurrentDirectoryAsync(path);
    }

    private async Task OpenFromArchiveAsync(FolderItem item)
    {
        if (await CopyOutAsync(item) is not { } copy)
            return;
        InfoRaised?.Invoke($"Opened a copy of {item.Name} — edits to it are not saved back into the zip.");
        await LaunchAsync(FolderItem.FromPath(copy));
    }

    private async Task OpenFromAdminAsync(FolderItem item)
    {
        if (_core.Admin is not { } admin)
            return;
        IsLoading = true;
        CommandResult<string> result;
        try
        {
            result = await admin.CopyToTempAsync(item.FullPath, long.MaxValue);
        }
        finally
        {
            IsLoading = false;
        }
        if (!result.IsOk || result.Data is null)
        {
            ErrorRaised?.Invoke(result.Message ?? $"Could not read {item.Name} as administrator.");
            return;
        }
        InfoRaised?.Invoke($"Opened a read-only copy of {item.Name} — it is not the original.");
        await LaunchAsync(FolderItem.FromPath(result.Data));
    }

    private async Task<string?> CopyOutAsync(FolderItem item)
    {
        IsLoading = true;
        try
        {
            CommandResult<string?> result = await _core.Actions.CopyOutOfArchiveAsync(new(item.FullPath));
            if (result is { IsOk: true, Data: { Length: > 0 } path })
                return path;
            ErrorRaised?.Invoke(result.Message ?? $"Could not read {item.Name} out of the zip.");
            return null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LaunchAsync(FolderItem item)
    {
        CommandResult<string?> result = await _core.Actions.LaunchFileAsync(new(item.FullPath));
        if (result.IsOk)
            return;

        string message = result.Message ?? "Could not open the file.";
        if (result.Reason == "launch_refused")
            ShowAppPicker(item, message, all: false);
        else
            ErrorRaised?.Invoke(message);
    }

    private readonly List<string> _backHistory = [];
    private readonly List<string> _forwardHistory = [];
    private bool _navigatingHistory;

    public bool CanGoBack => _backHistory.Count > 0;
    public bool CanGoForward => _forwardHistory.Count > 0;

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        if (_backHistory.Count == 0)
            return;
        string target = _backHistory[^1];
        string current = DirectoryListing.CurrentDir;
        await NavigateHistoryAsync(target);
        if (PathCompare.PathMatches(DirectoryListing.CurrentDir, target))
        {
            _backHistory.RemoveAt(_backHistory.Count - 1);
            _forwardHistory.Add(current);
            NotifyHistory();
        }
    }

    [RelayCommand]
    public async Task GoForwardAsync()
    {
        if (_forwardHistory.Count == 0)
            return;
        string target = _forwardHistory[^1];
        string current = DirectoryListing.CurrentDir;
        await NavigateHistoryAsync(target);
        if (PathCompare.PathMatches(DirectoryListing.CurrentDir, target))
        {
            _forwardHistory.RemoveAt(_forwardHistory.Count - 1);
            _backHistory.Add(current);
            NotifyHistory();
        }
    }

    private async Task NavigateHistoryAsync(string target)
    {
        _navigatingHistory = true;
        try
        {
            await SetCurrentDirectoryAsync(target);
        }
        finally
        {
            _navigatingHistory = false;
        }
    }

    public void GoUpDirectory()
    {
        string current = DirectoryListing.CurrentDir;
        CommandResult<FolderItem?> parent = _core.Actions.GetParent(new(current));
        if (!parent.IsOk)
        {
            ErrorRaised?.Invoke(parent.Message ?? "Could not read the parent directory.");
            return;
        }
        if (parent.Data is null)
        {
            ShowDrives();
            return;
        }
        _ = SetCurrentDirectoryAsync(parent.Data.FullPath, highlightPath: current);
    }

    private void ShowDrives() => DrivePickerRequested?.Invoke();

    public void RefreshDriveCommands()
    {
        CommandResult<DriveEntry[]> result = _core.Actions.ListDrives(new());
        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(result.Message ?? "Could not read the drive list.");
            return;
        }

        HashSet<string> live = [];
        foreach (DriveEntry drive in result.Data ?? [])
        {
            string id = CommandDef.DriveIdPrefix + drive.RootPath;
            string root = drive.RootPath;
            string title = drive.Label is null
                ? $"Go to Drive {root}"
                : $"Go to Drive {root} ({drive.Label})";
            live.Add(id);
            _registry.Register(new CommandDef(id, title, CommandKind.User),
                () => _ = SetCurrentDirectoryAsync(root));
        }

        foreach (string id in _registry.CommandIdsStartingWith(CommandDef.DriveIdPrefix))
        {
            if (!live.Contains(id))
                _registry.Unregister(id);
        }
    }
}

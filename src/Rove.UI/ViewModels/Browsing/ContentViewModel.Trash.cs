using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private void GoToTrash()
    {
        CommandResult<string?> result = _core.Actions.OpenTrash(new());
        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(result.Message ?? $"Could not open the {TrashService.DisplayName}.");
            return;
        }
        if (result.Data is { Length: > 0 } path)
            _ = SetCurrentDirectoryAsync(path);
        else
            InfoRaised?.Invoke($"Opened the {TrashService.DisplayName}.");
    }

    private void RestoreTrashedItems()
    {
        string[] paths = [.. Targets().Select(t => t.Item.FullPath)];
        if (paths.Length > 0)
            _ = RestoreTrashedItemsAsync(paths);
    }

    private void RestoreAllTrashedItems() => _ = RestoreAllTrashedItemsAsync();

    private async Task RestoreAllTrashedItemsAsync()
    {
        string[] paths = await TopLevelTrashPathsAsync();
        if (paths.Length == 0)
            return;
        await RestoreTrashedItemsAsync(paths);
        if (InTrash)
            _ = SetCurrentDirectoryAsync(TrashService.BrowsePath!);
    }

    private async Task<string[]> TopLevelTrashPathsAsync()
    {
        if (TrashService.BrowsePath is not { } root)
        {
            ErrorRaised?.Invoke($"The {TrashService.DisplayName} can't be listed on this system.");
            return [];
        }
        CommandResult<FolderItem[]> listing = await _core.Actions.ReadDirectoryAsync(new(root));
        if (!listing.IsOk || listing.Data is not { Length: > 0 } items)
        {
            InfoRaised?.Invoke($"The {TrashService.DisplayName} is empty.");
            return [];
        }
        return [.. items.Select(i => i.FullPath)];
    }

    private async Task RestoreTrashedItemsAsync(string[] paths)
    {
        string from = DirectoryListing.CurrentDir;
        ClearMarks();

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Putting back {Describe(paths.Length)}",
            indeterminate: true,
            (progress, ct) => _core.Actions.RestoreTrashedItemsAsync(new(paths), progress, ct));

        if (outcome is not { } result)
            return;

        List<PathPair> undone = [];
        foreach (OpResult op in (result.Data ?? []).Where(o => o.Ok && o.Item is not null))
            undone.Add(new PathPair(op.Path, op.Item!.FullPath));

        if (StillIn(from))
            RemoveFromListing(result.Data);
        _undo.Push(new(UndoAction.RemoveCopies, $"putting back of {Describe(undone.Count)}", undone));

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Put back"));
        else
            InfoRaised?.Invoke($"Put {Describe(undone.Count)} back.");
    }

    private void EmptyTrash()
    {
        if (TrashService.BrowsePath is null)
        {
            ErrorRaised?.Invoke($"The {TrashService.DisplayName} can't be emptied on this system.");
            return;
        }
        ConfirmRequested?.Invoke($"Empty the {TrashService.DisplayName}? This cannot be undone.",
            () => _ = EmptyTrashAsync());
    }

    private async Task EmptyTrashAsync()
    {
        string[] paths = await TopLevelTrashPathsAsync();
        if (paths.Length == 0)
            return;
        await DeleteItemsPermanentAsync(paths, paths.Length, DirectoryListing.CurrentDir);
        if (InTrash)
            _ = SetCurrentDirectoryAsync(TrashService.BrowsePath!);
    }
}

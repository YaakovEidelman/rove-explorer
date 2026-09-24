using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private void DeleteItems()
    {
        if (RefusedInArchive("Delete") || RefusedInAdminView("Delete"))
            return;
        if (InTrash)
        {
            InfoRaised?.Invoke($"Already in the {TrashService.DisplayName} — delete permanently instead.");
            return;
        }
        _ = DeleteItemsAsync();
    }

    private async Task DeleteItemsAsync()
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;

        string[] paths = [.. targets.Select(t => t.Item.FullPath)];
        string from = DirectoryListing.CurrentDir;
        ClearMarks();

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Deleting {Describe(targets.Count)}",
            indeterminate: true,
            (progress, ct) => _core.Actions.DeleteItemsAsync(new(paths), progress, ct));

        if (outcome is not { } result)
            return;
        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(result.Message ?? "Delete failed.");
            return;
        }

        if (StillIn(from))
            RemoveFromListing(result.Data);
        _undo.Push(new(UndoAction.RestoreFromTrash, $"delete of {Describe(paths.Length)}",
            [.. paths.Select(p => new PathPair(p, string.Empty))]));
        InfoRaised?.Invoke($"Sent {Describe(targets.Count)} to the {TrashService.DisplayName}.");
    }

    private void DeleteItemsPermanent()
    {
        if (RefusedInArchive("Delete") || RefusedInAdminView("Delete"))
            return;
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;

        string[] paths = [.. targets.Select(t => t.Item.FullPath)];
        string what = targets.Count == 1 ? targets[0].Name : $"{targets.Count} items";
        string from = DirectoryListing.CurrentDir;
        ConfirmRequested?.Invoke($"Permanently delete {what}? This cannot be undone.",
            () => _ = DeleteItemsPermanentAsync(paths, targets.Count, from));
    }

    private async Task DeleteItemsPermanentAsync(string[] paths, int count, string from)
    {
        ClearMarks();
        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Deleting {Describe(count)}",
            indeterminate: false,
            (progress, ct) => _core.Actions.DeleteItemsPermanentAsync(new(paths), progress, ct));

        if (outcome is not { } result)
            return;

        if (StillIn(from))
            RemoveFromListing(result.Data);
        int deleted = result.Data?.Count(r => r.Ok) ?? 0;
        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Delete"));
        else
            InfoRaised?.Invoke($"Permanently deleted {Describe(deleted)}.");
    }

    private void RemoveFromListing(OpResult[]? results)
    {
        if (results is null)
            return;
        foreach (OpResult op in results.Where(r => r.Ok))
            DirectoryListing.Remove(op.Path);
        DirectoryListing.ApplyView();
    }
}

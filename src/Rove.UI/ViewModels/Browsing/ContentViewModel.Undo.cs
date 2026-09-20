using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private void UndoLastAction() => _ = UndoLastActionAsync();

    private async Task UndoLastActionAsync()
    {
        if (_undo.Pop() is not { } step)
        {
            InfoRaised?.Invoke("Nothing to undo.");
            return;
        }

        switch (step.Action)
        {
            case UndoAction.RenameBack:
                UndoRename(step);
                break;
            case UndoAction.RemoveCreated:
                UndoCreate(step);
                break;
            case UndoAction.RemoveCopies:
            case UndoAction.MoveBack:
            case UndoAction.RestoreFromTrash:
                await UndoFileOperationAsync(step);
                break;
        }
    }

    private void UndoRename(UndoStep step)
    {
        PathPair pair = step.Items[0];
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(pair.Before));

        CommandResult<FolderItem?> result = _core.Actions.RenameItem(new(pair.After, name));
        if (!result.IsOk || result.Data is null)
        {
            ErrorRaised?.Invoke(result.Message ?? $"Could not undo the {step.Description}.");
            return;
        }

        if (StillIn(Path.GetDirectoryName(pair.After) ?? string.Empty))
        {
            DirectoryListing.Rename(pair.After, result.Data);
            DirectoryListing.ListSelection.SelectPath(result.Data.FullPath);
        }
        InfoRaised?.Invoke($"Undid the {step.Description}.");
    }

    private void UndoCreate(UndoStep step)
    {
        string path = step.Items[0].After;
        CommandResult<string?> result = _core.Actions.DeleteIfEmpty(new(path));

        if (!result.IsOk && result.Reason != "not_found")
        {
            ErrorRaised?.Invoke(result.Message ?? $"Could not undo the {step.Description}.");
            return;
        }

        DirectoryListing.Remove(path);
        DirectoryListing.ApplyView();
        InfoRaised?.Invoke($"Undid the {step.Description}.");
    }

    private async Task UndoFileOperationAsync(UndoStep step)
    {
        string here = DirectoryListing.CurrentDir;
        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Undoing the {step.Description}",
            indeterminate: step.Action == UndoAction.RestoreFromTrash,
            (progress, ct) => step.Action switch
            {
                UndoAction.RemoveCopies => _core.Actions.RemoveItemsAsync(
                    new([.. step.Items.Select(i => i.After)]), progress, ct),
                UndoAction.RestoreFromTrash => _core.Actions.RestoreItemsAsync(
                    new([.. step.Items.Select(i => i.Before)]), progress, ct),
                _ => MoveBackAsync(step, progress, ct),
            });

        if (outcome is not { } result)
            return;

        if (StillIn(here))
            await ReloadCurrentDirectoryAsync();

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Undo"));
        else
            InfoRaised?.Invoke($"Undid the {step.Description}.");
    }

    private async Task<CommandResult<OpResult[]>> MoveBackAsync(
        UndoStep step, IProgress<FileOpProgress> progress, CancellationToken ct
    )
    {
        List<OpResult> all = [];
        IEnumerable<IGrouping<string, PathPair>> byFolder = step.Items
            .GroupBy(i => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(i.Before)) ?? string.Empty,
                PathCompare.Comparer);

        foreach (IGrouping<string, PathPair> folder in byFolder)
        {
            if (folder.Key.Length == 0)
                continue;
            CommandResult<OpResult[]> result = await _core.Actions.MoveItemsAsync(
                new([.. folder.Select(i => i.After)], folder.Key, Overwrite: false), progress, ct);
            all.AddRange(result.Data ?? []);
        }

        int failed = all.Count(r => !r.Ok);
        if (failed == 0)
            return CommandResult<OpResult[]>.Ok([.. all]);

        string message = string.Join("; ", all.Where(r => !r.Ok).Select(r => r.Message ?? r.Reason).Distinct());
        string reason = failed == all.Count ? all.First(r => !r.Ok).Reason : "partial_failure";
        return CommandResult<OpResult[]>.Fail(reason, message, [.. all]);
    }
}

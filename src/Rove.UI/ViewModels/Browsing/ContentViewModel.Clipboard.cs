using Rove.Core.Protocol;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private void CopyItems()
    {
        if (!RefusedInArchive("Copy") && !RefusedInAdminView("Copy"))
            SetClipboard(ClipboardOp.Copy);
    }

    private void CutItems()
    {
        if (!RefusedInArchive("Cut") && !RefusedInTrash("Cut") && !RefusedInAdminView("Cut"))
            SetClipboard(ClipboardOp.Cut);
    }

    private void CopyToHereItems()
    {
        if (!RefusedInArchive("Copy") && !RefusedInAdminView("Copy"))
            SetClipboard(ClipboardOp.Copy, oneShot: true);
    }

    private void MoveToHereItems()
    {
        if (!RefusedInArchive("Cut") && !RefusedInTrash("Cut") && !RefusedInAdminView("Cut"))
            SetClipboard(ClipboardOp.Cut, oneShot: true);
    }

    private void SetClipboard(ClipboardOp op, bool oneShot = false)
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;
        string[] paths = [.. targets.Select(t => t.Item.FullPath)];
        Guid token = Guid.NewGuid();
        _clipboard.Set(op, paths, oneShot, token);
        _ = _systemClipboard.SetFilesAsync(paths, op, token);
        ClearMarks();
        string verb = op == ClipboardOp.Copy ? "Copied" : "Cut";
        InfoRaised?.Invoke($"{verb} {targets.Count} item{Plural(targets.Count)}.");
    }

    private void PasteItems()
    {
        if (!RefusedInArchive("Paste") && !RefusedInTrash("Paste") && !RefusedInAdminView("Paste"))
            _ = PasteItemsAsync();
    }

    private async Task PasteItemsAsync()
    {
        string[] paths;
        ClipboardOp clipboardOp;
        bool oneShot = false;
        (IReadOnlyList<string> Paths, ClipboardOp Op, Guid? Token)? external =
            await _systemClipboard.TryGetFilesAsync();
        bool internalStillOnSystemClipboard = _clipboard.HasItems && external?.Token == _clipboard.Token;

        if (internalStillOnSystemClipboard)
        {
            paths = [.. _clipboard.Paths];
            clipboardOp = _clipboard.Op;
            oneShot = _clipboard.OneShot;
        }
        else if (external is { } ext)
        {
            paths = [.. ext.Paths];
            clipboardOp = ext.Op;
            _clipboard.Clear();
        }
        else if (_clipboard.HasItems)
        {
            paths = [.. _clipboard.Paths];
            clipboardOp = _clipboard.Op;
            oneShot = _clipboard.OneShot;
        }
        else
        {
            InfoRaised?.Invoke("Nothing to paste.");
            return;
        }

        string target = DirectoryListing.CurrentDir;
        bool copying = clipboardOp == ClipboardOp.Copy;
        string verb = copying ? "Copying" : "Moving";

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"{verb} {Describe(paths.Length)}",
            indeterminate: false,
            (progress, ct) => copying
                ? _core.Actions.CopyItemsAsync(new(paths, target, Overwrite: false), progress, ct)
                : _core.Actions.MoveItemsAsync(new(paths, target, Overwrite: false), progress, ct));

        if (outcome is not { } result)
            return;

        int succeeded = 0;
        bool sameFolder = StillIn(target);
        List<PathPair> undone = [];
        if (result.Data is { } ops)
        {
            foreach (OpResult op in ops.Where(o => o.Ok))
            {
                succeeded++;
                if (op.Item is not null)
                    undone.Add(new PathPair(op.Path, op.Item.FullPath));
                if (op.Item is not null && sameFolder)
                    DirectoryListing.Upsert(op.Item);
            }
        }

        _undo.Push(new(
            copying ? UndoAction.RemoveCopies : UndoAction.MoveBack,
            $"paste of {Describe(undone.Count)}",
            undone));

        if (!copying || oneShot)
        {
            _clipboard.Clear();
            _ = _systemClipboard.ClearAsync();
        }

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Paste"));
        else
            InfoRaised?.Invoke($"Pasted {Describe(succeeded)}.");
    }

    private void RefreshCutFlags()
    {
        bool isCut = _clipboard.HasItems && _clipboard.Op == ClipboardOp.Cut;
        HashSet<string> cutPaths = isCut
            ? new(_clipboard.Paths, StringComparer.OrdinalIgnoreCase)
            : [];
        foreach (ListViewItem item in DirectoryListing.Items)
            item.IsCut = isCut && cutPaths.Contains(item.Item.FullPath);
    }

    private void DuplicateItems()
    {
        if (RefusedInArchive("Duplicate") || RefusedInTrash("Duplicate") || RefusedInAdminView("Duplicate"))
            return;
        _ = DuplicateItemsAsync();
    }

    private async Task DuplicateItemsAsync()
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;
        string[] paths = [.. targets.Select(t => t.Item.FullPath)];

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Duplicating {Describe(paths.Length)}",
            indeterminate: false,
            (progress, ct) => _core.Actions.DuplicateItemsAsync(new(paths), progress, ct));

        if (outcome is not { } result)
            return;

        int succeeded = 0;
        List<PathPair> created = [];
        if (result.Data is { } ops)
        {
            foreach (OpResult op in ops.Where(o => o.Ok))
            {
                succeeded++;
                if (op.Item is not null)
                {
                    DirectoryListing.Upsert(op.Item);
                    created.Add(new PathPair(op.Path, op.Item.FullPath));
                }
            }
        }

        ClearMarks();
        _undo.Push(new(UndoAction.RemoveCopies, $"duplicate of {Describe(succeeded)}", created));

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Duplicate"));
        else
            InfoRaised?.Invoke($"Duplicated {Describe(succeeded)}.");
    }

    private void CopyPath()
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
        {
            _ = _systemClipboard.CopyTextAsync(DirectoryListing.CurrentDir);
            InfoRaised?.Invoke("Copied folder path.");
            return;
        }
        string text = string.Join(Environment.NewLine, targets.Select(t => t.Item.FullPath));
        _ = _systemClipboard.CopyTextAsync(text);
        InfoRaised?.Invoke($"Copied {targets.Count} path{Plural(targets.Count)}.");
    }
}

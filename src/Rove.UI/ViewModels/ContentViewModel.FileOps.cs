using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rove.Core;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rove.UI.Models;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    // ── delete ───────────────────────────────────────────────────────────

    private void DeleteItems()
    {
        if (RefusedInArchive("Delete"))
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

        // The Windows Recycle Bin call is one shell batch that can't report or stop
        // partway, so the bar shows movement rather than a count.
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
        if (RefusedInArchive("Delete"))
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

    // ── the trash ────────────────────────────────────────────────────────
    // On Linux the trash is a folder like any other, so "go to the trash"
    // walks into it and everything else — opening, deleting for good — works
    // there as it does anywhere. Putting something back reads the record the
    // trash keeps of where it came from, so it goes home rather than here.

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

    /// <summary>The immediate contents of the trash root — never wherever a nested view has drilled to.</summary>
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

    // ── emptying the trash ───────────────────────────────────────────────

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

    // ── clipboard verbs ──────────────────────────────────────────────────

    private void CopyItems()
    {
        if (!RefusedInArchive("Copy"))
            SetClipboard(ClipboardOp.Copy);
    }

    private void CutItems()
    {
        if (!RefusedInArchive("Cut") && !RefusedInTrash("Cut"))
            SetClipboard(ClipboardOp.Cut);
    }

    private void SetClipboard(ClipboardOp op)
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;
        string[] paths = [.. targets.Select(t => t.Item.FullPath)];
        _clipboard.Set(op, paths);
        _ = _systemClipboard.SetFilesAsync(paths, op);
        ClearMarks();
        string verb = op == ClipboardOp.Copy ? "Copied" : "Cut";
        InfoRaised?.Invoke($"{verb} {targets.Count} item{Plural(targets.Count)}.");
    }

    private void PasteItems()
    {
        if (!RefusedInArchive("Paste") && !RefusedInTrash("Paste"))
            _ = PasteItemsAsync();
    }

    private async Task PasteItemsAsync()
    {
        string[] paths;
        ClipboardOp clipboardOp;
        (IReadOnlyList<string> Paths, ClipboardOp Op)? external = await _systemClipboard.TryGetFilesAsync();
        bool internalStillOnSystemClipboard = _clipboard.HasItems
            && external is { } current
            && SamePaths(current.Paths, _clipboard.Paths);

        if (internalStillOnSystemClipboard)
        {
            paths = [.. _clipboard.Paths];
            clipboardOp = _clipboard.Op;
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
                // The user may have walked somewhere else while this ran; the
                // rows belong to the folder that was pasted into, not this one.
                if (op.Item is not null && sameFolder)
                    DirectoryListing.Upsert(op.Item);
            }
        }

        _undo.Push(new(
            copying ? UndoAction.RemoveCopies : UndoAction.MoveBack,
            $"paste of {Describe(undone.Count)}",
            undone));

        if (!copying)
            _clipboard.Clear();

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Paste"));
        else
            InfoRaised?.Invoke($"Pasted {Describe(succeeded)}.");
    }

    private static bool SamePaths(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
            return false;
        HashSet<string> set = new(a, StringComparer.OrdinalIgnoreCase);
        return set.SetEquals(b);
    }

    /// <summary>Dim items sitting in the cut clipboard so "cut" is visible state.</summary>
    private void RefreshCutFlags()
    {
        bool isCut = _clipboard.HasItems && _clipboard.Op == ClipboardOp.Cut;
        HashSet<string> cutPaths = isCut
            ? new(_clipboard.Paths, StringComparer.OrdinalIgnoreCase)
            : [];
        foreach (ListViewItem item in DirectoryListing.Items)
            item.IsCut = isCut && cutPaths.Contains(item.Item.FullPath);
    }

    private void CopyPath()
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
        {
            // No highlight — copy the directory itself.
            _ = _systemClipboard.CopyTextAsync(DirectoryListing.CurrentDir);
            InfoRaised?.Invoke("Copied folder path.");
            return;
        }
        string text = string.Join(Environment.NewLine, targets.Select(t => t.Item.FullPath));
        _ = _systemClipboard.CopyTextAsync(text);
        InfoRaised?.Invoke($"Copied {targets.Count} path{Plural(targets.Count)}.");
    }

    // ── extract ──────────────────────────────────────────────────────────

    private void ExtractItems()
    {
        if (!RefusedInArchive("Extract") && !RefusedInTrash("Extract"))
            _ = ExtractItemsAsync();
    }

    private async Task ExtractItemsAsync()
    {
        string[] paths = [.. Targets()
            .Where(t => !t.Item.IsDirectory && ArchiveService.IsArchive(t.Item.FullPath))
            .Select(t => t.Item.FullPath)];

        if (paths.Length == 0)
        {
            InfoRaised?.Invoke("Nothing to extract — highlight or mark a .zip file.");
            return;
        }

        string target = DirectoryListing.CurrentDir;
        ClearMarks();

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Extracting {Describe(paths.Length)}",
            indeterminate: false,
            (progress, ct) => _core.Actions.ExtractArchivesAsync(new(paths, target), progress, ct));

        if (outcome is not { } result)
            return;

        int succeeded = 0;
        bool sameFolder = StillIn(target);
        List<PathPair> undone = [];
        foreach (OpResult op in (result.Data ?? []).Where(o => o.Ok && o.Item is not null))
        {
            succeeded++;
            undone.Add(new PathPair(op.Path, op.Item!.FullPath));
            if (sameFolder)
                DirectoryListing.Upsert(op.Item);
        }

        _undo.Push(new(UndoAction.RemoveCopies, $"extract of {Describe(undone.Count)}", undone));

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Extract"));
        else
            InfoRaised?.Invoke($"Extracted {Describe(succeeded)}.");
    }

    // ── compress ─────────────────────────────────────────────────────────

    private void CompressItems()
    {
        if (!RefusedInArchive("Compress") && !RefusedInTrash("Compress"))
            _ = CompressItemsAsync();
    }

    private async Task CompressItemsAsync()
    {
        string[] paths = [.. Targets().Select(t => t.Item.FullPath)];
        if (paths.Length == 0)
        {
            InfoRaised?.Invoke("Nothing to compress — highlight or mark something first.");
            return;
        }

        string target = DirectoryListing.CurrentDir;
        ClearMarks();

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Compressing {Describe(paths.Length)}",
            indeterminate: false,
            (progress, ct) => _core.Actions.CompressItemsAsync(new(paths, target), progress, ct));

        if (outcome is not { } result)
            return;

        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(DescribeFailure(result, "Compress"));
            return;
        }

        OpResult made = (result.Data ?? [])[0];
        // Not RemoveCreated: that one only takes back something still empty,
        // and a zip that worked is the opposite of empty.
        _undo.Push(new(UndoAction.RemoveCopies, $"zip {Path.GetFileName(made.Path)}",
            [new PathPair(string.Empty, made.Path)]));

        if (StillIn(target) && made.Item is { } item)
        {
            DirectoryListing.Upsert(item);
            DirectoryListing.ListSelection.SelectPath(item.FullPath);
        }
        InfoRaised?.Invoke($"Compressed {Describe(paths.Length)} into {Path.GetFileName(made.Path)}.");
    }
}

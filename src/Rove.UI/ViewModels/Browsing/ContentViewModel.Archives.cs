using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private void ExtractItems()
    {
        if (!RefusedInArchive("Extract") && !RefusedInTrash("Extract") && !RefusedInAdminView("Extract"))
            _ = ExtractItemsAsync();
    }

    private bool HasArchiveTarget() =>
        Targets().Any(t => !t.Item.IsDirectory && ArchiveService.IsArchive(t.Item.FullPath));

    private async Task ExtractItemsAsync()
    {
        string[] paths = [.. Targets()
            .Where(t => !t.Item.IsDirectory && ArchiveService.IsArchive(t.Item.FullPath))
            .Select(t => t.Item.FullPath)];

        if (paths.Length == 0)
        {
            InfoRaised?.Invoke("Nothing to extract — highlight or mark an archive.");
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

    private void CompressItems()
    {
        if (!RefusedInArchive("Compress") && !RefusedInTrash("Compress") && !RefusedInAdminView("Compress"))
            _ = CompressItemsAsync(ArchiveFormat.Zip);
    }

    private void CompressItemsTarGz()
    {
        if (!RefusedInArchive("Compress") && !RefusedInTrash("Compress") && !RefusedInAdminView("Compress"))
            _ = CompressItemsAsync(ArchiveFormat.TarGz);
    }

    private async Task CompressItemsAsync(ArchiveFormat format)
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
            (progress, ct) => _core.Actions.CompressItemsAsync(new(paths, target, format), progress, ct));

        if (outcome is not { } result)
            return;

        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(DescribeFailure(result, "Compress"));
            return;
        }

        OpResult made = (result.Data ?? [])[0];
        _undo.Push(new(UndoAction.RemoveCopies, $"archive {Path.GetFileName(made.Path)}",
            [new PathPair(string.Empty, made.Path)]));

        if (StillIn(target) && made.Item is { } item)
        {
            DirectoryListing.Upsert(item);
            DirectoryListing.ListSelection.SelectPath(item.FullPath);
        }
        InfoRaised?.Invoke($"Compressed {Describe(paths.Length)} into {Path.GetFileName(made.Path)}.");
    }
}

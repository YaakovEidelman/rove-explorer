using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
    public CommandResult<OpResult[]> DeleteItems(DeleteItemsArgs args)
    {
        CommandResult<string?> trash = TrashService.MoveToTrash(args.Paths);
        if (!trash.IsOk)
            return CommandResult<OpResult[]>.Fail(trash.Reason, trash.Message);

        OpResult[] results = [.. args.Paths.Select(p => OpResult.Success(p))];
        return CommandResult<OpResult[]>.Ok(results);
    }

    public Task<CommandResult<OpResult[]>> DeleteItemsAsync(
        DeleteItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() =>
    {
        progress?.Report(new FileOpProgress("Deleting", 0, args.Paths.Length, ""));
        CommandResult<OpResult[]> result = DeleteItems(args);
        progress?.Report(new FileOpProgress("Deleting", args.Paths.Length, args.Paths.Length, ""));
        return result;
    }, CancellationToken.None);

    public CommandResult<OpResult[]> DeleteItemsPermanent(DeleteItemsArgs args) =>
        DeleteItemsPermanent(args, null, CancellationToken.None);

    public Task<CommandResult<OpResult[]>> DeleteItemsPermanentAsync(
        DeleteItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => DeleteItemsPermanent(args, progress, ct), CancellationToken.None);

    private static CommandResult<OpResult[]> DeleteItemsPermanent(
        DeleteItemsArgs args, IProgress<FileOpProgress>? progress, CancellationToken ct
    ) => RunBatch("Deleting", args.Paths, progress, ct,
            (path, ticker, index) => DeleteOnePermanent(path, ct), null);

    private static OpResult DeleteOnePermanent(string itemPath, CancellationToken ct)
    {
        string path = LongPath.ForIo(itemPath);
        try
        {
            ct.ThrowIfCancellationRequested();
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                TrashService.ForgetRecord(itemPath);
                return OpResult.Success(itemPath);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
                TrashService.ForgetRecord(itemPath);
                return OpResult.Success(itemPath);
            }
            return OpResult.Failure(itemPath, "not_found", $"No such item: {itemPath}");
        }
        catch (OperationCanceledException)
        {
            return OpResult.Failure(itemPath, "cancelled", "Cancelled.");
        }
        catch (UnauthorizedAccessException)
        {
            return OpResult.Failure(itemPath, "permission_denied", $"Access denied: {itemPath}");
        }
        catch (PathTooLongException ex)
        {
            return OpResult.Failure(itemPath, "path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return OpResult.Failure(itemPath, "io_error", ex.Message);
        }
    }

    public Task<CommandResult<OpResult[]>> RemoveItemsAsync(
        RestoreItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    )
    {
        string[] present = [.. args.Paths.Where(p => Exists(LongPath.ForIo(p)))];
        if (present.Length == 0)
            return Task.FromResult(CommandResult<OpResult[]>.Ok(
                [.. args.Paths.Select(p => OpResult.Success(p))]));

        bool toTrash = TrashService.IsSupported
            && !present.Any(LongPath.NeedsExtendedForm);

        return toTrash
            ? DeleteItemsAsync(new(present), progress, ct)
            : DeleteItemsPermanentAsync(new(present), progress, ct);
    }

    public CommandResult<string?> DeleteIfEmpty(DeleteIfEmptyArgs args)
    {
        string path = LongPath.ForIo(args.Path);
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(args.Path)));
        try
        {
            if (Directory.Exists(path))
            {
                if (Directory.EnumerateFileSystemEntries(path).Any())
                    return CommandResult<string?>.Fail("not_empty", $"{name} is not empty any more.");
                Directory.Delete(path);
                return CommandResult<string?>.Ok(null);
            }
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 0)
                    return CommandResult<string?>.Fail("not_empty", $"{name} has been written to since.");
                File.Delete(path);
                return CommandResult<string?>.Ok(null);
            }
            return CommandResult<string?>.Fail("not_found", $"No such item: {args.Path}");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<string?>.Fail("permission_denied", $"Access denied: {args.Path}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<string?>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<string?>.Fail("io_error", ex.Message);
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
    public CommandResult<OpResult[]> RestoreItems(RestoreItemsArgs args)
    {
        CommandResult<string[]> restore = TrashService.RestoreFromTrash(args.Paths);
        if (!restore.IsOk)
            return CommandResult<OpResult[]>.Fail(restore.Reason, restore.Message);

        HashSet<string> back = new(restore.Data ?? [], PathCompare.Comparer);
        OpResult[] results = [.. args.Paths.Select(path => back.Contains(path)
            ? OpResult.Success(path, FolderItem.FromPath(path))
            : OpResult.Failure(path, "not_restored",
                $"{Path.GetFileName(path)} is no longer in the {TrashService.DisplayName}."))];
        return Summarize(results);
    }

    public Task<CommandResult<OpResult[]>> RestoreItemsAsync(
        RestoreItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() =>
    {
        progress?.Report(new FileOpProgress("Restoring", 0, args.Paths.Length, ""));
        CommandResult<OpResult[]> result = RestoreItems(args);
        progress?.Report(new FileOpProgress("Restoring", args.Paths.Length, args.Paths.Length, ""));
        return result;
    }, CancellationToken.None);

    public CommandResult<string?> OpenTrash(OpenTrashArgs args)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true });
                return CommandResult<string?>.Ok(null);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                return CommandResult<string?>.Fail("launch_failed", ex.Message);
            }
        }

        if (TrashService.BrowsePath is not { } path)
            return CommandResult<string?>.Fail("trash_unsupported", "There is no trash to open on this system.");

        try
        {
            Directory.CreateDirectory(LongPath.ForIo(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CommandResult<string?>.Fail("trash_failed", $"Could not open the trash at {path} ({ex.Message}).");
        }
        return CommandResult<string?>.Ok(path);
    }

    public CommandResult<OpResult[]> RestoreTrashedItems(RestoreItemsArgs args)
    {
        CommandResult<OpResult[]> restored = TrashService.RestoreTrashedPaths(args.Paths);
        return restored.IsOk && restored.Data is { } results
            ? Summarize(results)
            : restored;
    }

    public Task<CommandResult<OpResult[]>> RestoreTrashedItemsAsync(
        RestoreItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() =>
    {
        progress?.Report(new FileOpProgress("Restoring", 0, args.Paths.Length, ""));
        CommandResult<OpResult[]> result = RestoreTrashedItems(args);
        progress?.Report(new FileOpProgress("Restoring", args.Paths.Length, args.Paths.Length, ""));
        return result;
    }, CancellationToken.None);
}

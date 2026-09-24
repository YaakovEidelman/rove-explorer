using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
    public CommandResult<OpResult[]> MoveItems(MoveItemsArgs args) =>
        MoveItems(args, null, CancellationToken.None);

    public Task<CommandResult<OpResult[]>> MoveItemsAsync(
        MoveItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => MoveItems(args, progress, ct), CancellationToken.None);

    private static CommandResult<OpResult[]> MoveItems(
        MoveItemsArgs args, IProgress<FileOpProgress>? progress, CancellationToken ct
    ) => RunBatch("Moving", args.Paths, progress, ct,
            (source, ticker, index) => MoveOne(source, args.TargetDirectory, args.Overwrite, ticker, index, ct),
            () => Directory.Exists(LongPath.ForIo(args.TargetDirectory))
                ? null
                : CommandResult<OpResult[]>.Fail("not_found", $"Target directory does not exist: {args.TargetDirectory}"));

    private static OpResult MoveOne(
        string sourcePath, string targetDir, bool overwrite, ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(sourcePath)));
        string destPath = Path.Combine(LongPath.Display(targetDir), name);
        string source = LongPath.ForIo(sourcePath);
        string dest = LongPath.ForIo(destPath);

        if (PathCompare.PathMatches(Path.GetFullPath(source), Path.GetFullPath(dest)))
            return OpResult.Success(sourcePath, null);

        try
        {
            ct.ThrowIfCancellationRequested();
            if (Directory.Exists(source))
            {
                if (PathGuard.IsSameOrDescendant(sourcePath, targetDir))
                    return OpResult.Failure(sourcePath, "invalid_target", "Cannot move a folder into itself.");
                if (Exists(dest))
                    return OpResult.Failure(sourcePath, "already_exists", $"{name} already exists at the destination.");
                MoveDirectory(source, dest, ticker, index, ct);
                return OpResult.Success(sourcePath, FolderItem.From(new DirectoryInfo(dest)));
            }
            if (File.Exists(source))
            {
                if (Directory.Exists(dest))
                    return OpResult.Failure(sourcePath, "already_exists", $"A folder named {name} already exists at the destination.");
                if (File.Exists(dest) && !overwrite)
                    return OpResult.Failure(sourcePath, "already_exists", $"{name} already exists at the destination.");
                File.Move(source, dest, overwrite);
                return OpResult.Success(sourcePath, FolderItem.From(new FileInfo(dest)));
            }
            return OpResult.Failure(sourcePath, "not_found", $"No such item: {sourcePath}");
        }
        catch (OperationCanceledException)
        {
            return OpResult.Failure(sourcePath, "cancelled", $"{name} was cancelled.");
        }
        catch (UnauthorizedAccessException)
        {
            return OpResult.Failure(sourcePath, "permission_denied", $"Access denied: {sourcePath}");
        }
        catch (PathTooLongException ex)
        {
            return OpResult.Failure(sourcePath, "path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return OpResult.Failure(sourcePath, "io_error", ex.Message);
        }
    }

    private static void MoveDirectory(string source, string dest, ProgressTicker ticker, int index, CancellationToken ct)
    {
        if (string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), Path.GetPathRoot(Path.GetFullPath(dest)),
                StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(source, dest);
            return;
        }
        CopyDirectory(source, dest, overwriteFiles: false, "Moving", ticker, index, ct);
        Directory.Delete(source, recursive: true);
    }

    public CommandResult<OpResult[]> CopyItems(CopyItemsArgs args) =>
        CopyItems(args, null, CancellationToken.None);

    public Task<CommandResult<OpResult[]>> CopyItemsAsync(
        CopyItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => CopyItems(args, progress, ct), CancellationToken.None);

    private static CommandResult<OpResult[]> CopyItems(
        CopyItemsArgs args, IProgress<FileOpProgress>? progress, CancellationToken ct
    ) => RunBatch("Copying", args.Paths, progress, ct,
            (source, ticker, index) => CopyOne(source, args.TargetDirectory, args.Overwrite, ticker, index, ct),
            () => Directory.Exists(LongPath.ForIo(args.TargetDirectory))
                ? null
                : CommandResult<OpResult[]>.Fail("not_found", $"Target directory does not exist: {args.TargetDirectory}"));

    private static OpResult CopyOne(
        string sourcePath, string targetDir, bool overwrite, ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(sourcePath)));
        string destPath = Path.Combine(LongPath.Display(targetDir), name);
        return CopyToDest(sourcePath, destPath, overwrite, ticker, index, ct);
    }

    public CommandResult<OpResult[]> DuplicateItems(DuplicateItemsArgs args) =>
        DuplicateItems(args, null, CancellationToken.None);

    public Task<CommandResult<OpResult[]>> DuplicateItemsAsync(
        DuplicateItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => DuplicateItems(args, progress, ct), CancellationToken.None);

    private static CommandResult<OpResult[]> DuplicateItems(
        DuplicateItemsArgs args, IProgress<FileOpProgress>? progress, CancellationToken ct
    ) => RunBatch("Duplicating", args.Paths, progress, ct,
            (source, ticker, index) => DuplicateOne(source, ticker, index, ct), precondition: null);

    private static OpResult DuplicateOne(string sourcePath, ProgressTicker ticker, int index, CancellationToken ct)
    {
        string source = LongPath.ForIo(sourcePath);
        if (!Exists(source))
            return OpResult.Failure(sourcePath, "not_found", $"No such item: {sourcePath}");

        string destPath = UniqueDuplicateName(sourcePath);
        return CopyToDest(sourcePath, destPath, overwrite: false, ticker, index, ct);
    }

    private static string UniqueDuplicateName(string sourcePath)
    {
        string display = LongPath.Display(sourcePath);
        string? dir = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(display));
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(display));
        bool isDir = Directory.Exists(LongPath.ForIo(sourcePath));
        string stem = isDir ? name : Path.GetFileNameWithoutExtension(name);
        string ext = isDir ? "" : Path.GetExtension(name);

        string candidate = $"{stem} (copy){ext}";
        for (int n = 2; Exists(LongPath.ForIo(Path.Combine(dir ?? "", candidate))); n++)
            candidate = $"{stem} (copy {n}){ext}";

        return Path.Combine(dir ?? "", candidate);
    }

    private static OpResult CopyToDest(
        string sourcePath, string destPath, bool overwrite, ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(sourcePath)));
        string source = LongPath.ForIo(sourcePath);
        string dest = LongPath.ForIo(destPath);

        try
        {
            ct.ThrowIfCancellationRequested();
            if (Directory.Exists(source))
            {
                string destDir = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(dest)) ?? dest;
                if (PathGuard.IsSameOrDescendant(sourcePath, destDir))
                    return OpResult.Failure(sourcePath, "invalid_target", "Cannot copy a folder into itself.");
                if (Exists(dest))
                    return OpResult.Failure(sourcePath, "already_exists", $"{name} already exists at the destination.");
                try
                {
                    CopyDirectory(source, dest, overwrite, "Copying", ticker, index, ct);
                }
                catch (OperationCanceledException)
                {
                    TryDeleteTree(dest);
                    throw;
                }
                return OpResult.Success(sourcePath, FolderItem.From(new DirectoryInfo(dest)));
            }
            if (File.Exists(source))
            {
                if (PathCompare.PathMatches(Path.GetFullPath(source), Path.GetFullPath(dest)))
                    return OpResult.Failure(sourcePath, "invalid_target", "Source and destination are the same file.");
                if (Directory.Exists(dest))
                    return OpResult.Failure(sourcePath, "already_exists", $"A folder named {name} already exists at the destination.");
                if (File.Exists(dest) && !overwrite)
                    return OpResult.Failure(sourcePath, "already_exists", $"{name} already exists at the destination.");
                File.Copy(source, dest, overwrite);
                return OpResult.Success(sourcePath, FolderItem.From(new FileInfo(dest)));
            }
            return OpResult.Failure(sourcePath, "not_found", $"No such item: {sourcePath}");
        }
        catch (OperationCanceledException)
        {
            return OpResult.Failure(sourcePath, "cancelled", $"{name} was cancelled.");
        }
        catch (UnauthorizedAccessException)
        {
            return OpResult.Failure(sourcePath, "permission_denied", $"Access denied: {sourcePath}");
        }
        catch (PathTooLongException ex)
        {
            return OpResult.Failure(sourcePath, "path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return OpResult.Failure(sourcePath, "io_error", ex.Message);
        }
    }

    private static void CopyDirectory(
        string source, string dest, bool overwriteFiles,
        string verb, ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(dest);
        DirectoryInfo dir = new(source);
        foreach (FileInfo file in dir.EnumerateFiles())
        {
            ct.ThrowIfCancellationRequested();
            ticker.Report(verb, index, -1, file.Name);
            string fileDest = LongPath.ForIo(Path.Combine(LongPath.Display(dest), file.Name));
            if (file.LinkTarget is { } fileLink)
                File.CreateSymbolicLink(fileDest, fileLink);
            else
                file.CopyTo(fileDest, overwriteFiles);
        }
        foreach (DirectoryInfo sub in dir.EnumerateDirectories())
        {
            ct.ThrowIfCancellationRequested();
            string subDest = LongPath.ForIo(Path.Combine(LongPath.Display(dest), sub.Name));
            if (sub.LinkTarget is { } dirLink)
            {
                ticker.Report(verb, index, -1, sub.Name);
                Directory.CreateSymbolicLink(subDest, dirLink);
            }
            else
            {
                CopyDirectory(sub.FullName, subDest, overwriteFiles, verb, ticker, index, ct);
            }
        }
    }
}

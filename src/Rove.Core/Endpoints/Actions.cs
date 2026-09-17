using System.ComponentModel;
using System.Diagnostics;
using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public class Actions
{
    private readonly IIconFetcher _fetcher;

    public Actions()
    {
        _fetcher = IconFetcherChooser.CreateForHost();
    }

    // ── READ_DIR ─────────────────────────────────────────────────────────

    public CommandResult<FolderItem[]> ReadDirectory(ReadDirectoryArgs args)
    {
        if (ArchivePath.TryParse(args.Path, out ArchivePath inside))
            return ReadInsideArchive(inside);

        try
        {
            DirectoryInfo dirInfo = new(LongPath.ForIo(args.Path));
            IEnumerable<FileSystemInfo> raw = dirInfo.EnumerateFileSystemInfos();
            FolderItem[] items = [.. FolderItem.From(raw)];
            return CommandResult<FolderItem[]>.Ok(items);
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem[]>.Fail("permission_denied", $"Access denied: {args.Path}");
        }
        catch (DirectoryNotFoundException)
        {
            return CommandResult<FolderItem[]>.Fail("not_found", $"Directory does not exist: {args.Path}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<FolderItem[]>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<FolderItem[]>.Fail("io_error", ex.Message);
        }
    }

    public Task<CommandResult<FolderItem[]>> ReadDirectoryAsync(ReadDirectoryArgs args) =>
        Task.Run(() => ReadDirectory(args));

    /// <summary>
    /// A folder inside a zip lists like any other folder. What can go wrong
    /// is different, though: the archive can be gone, or damaged, which no
    /// amount of reading a real directory ever produces.
    /// </summary>
    private static CommandResult<FolderItem[]> ReadInsideArchive(ArchivePath location)
    {
        try
        {
            return CommandResult<FolderItem[]>.Ok(ArchiveBrowser.Children(location));
        }
        catch (FileNotFoundException)
        {
            return CommandResult<FolderItem[]>.Fail("not_found", $"There is no zip at {location.Archive}.");
        }
        catch (InvalidDataException)
        {
            return CommandResult<FolderItem[]>.Fail(
                "bad_archive", $"{location.Name} is damaged, or is not really a zip file.");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem[]>.Fail("permission_denied", $"Access denied: {location.Archive}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<FolderItem[]>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<FolderItem[]>.Fail("io_error", ex.Message);
        }
    }

    // ── GET_PARENT ───────────────────────────────────────────────────────

    public CommandResult<FolderItem?> GetParent(GetParentArgs args)
    {
        // Inside a zip, "up" walks back out through the archive first, and
        // only leaves it once there is nothing left above.
        if (ArchivePath.TryParse(args.Path, out ArchivePath inside))
        {
            return inside.Parent is { } above
                ? CommandResult<FolderItem?>.Ok(ArchiveBrowser.Folder(above))
                : ParentOnDisk(inside.Archive);
        }
        return ParentOnDisk(args.Path);
    }

    private static CommandResult<FolderItem?> ParentOnDisk(string path)
    {
        try
        {
            DirectoryInfo? parent = Directory.GetParent(LongPath.ForIo(path));
            if (parent is null)
                return CommandResult<FolderItem?>.Ok(null); // already at a root
            return CommandResult<FolderItem?>.Ok(FolderItem.From(parent));
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem?>.Fail("permission_denied", $"Access denied: {path}");
        }
    }

    // ── RESOLVE_PATH ─────────────────────────────────────────────────────
    // What the path bar hands back: the item a typed path names, whether that
    // is a folder to open or a file to point at.

    public CommandResult<FolderItem?> ResolvePath(ResolvePathArgs args)
    {
        if (PathResolver.Resolve(args.Input, args.CurrentDirectory) is not { } path)
            return CommandResult<FolderItem?>.Fail("invalid_path", $"That is not a path: {args.Input}");

        string io = LongPath.ForIo(path);
        try
        {
            if (Directory.Exists(io))
                return CommandResult<FolderItem?>.Ok(FolderItem.From(new DirectoryInfo(io)));
            if (File.Exists(io))
                return CommandResult<FolderItem?>.Ok(FolderItem.From(new FileInfo(io)));
            // Nothing on disk goes by that name, but it may still name
            // something real inside a zip along the way.
            if (ArchivePath.TryParse(path, out ArchivePath inside)
                && ArchiveBrowser.Describe(inside) is { } entry)
            {
                return CommandResult<FolderItem?>.Ok(entry);
            }
            return CommandResult<FolderItem?>.Fail("not_found", $"There is nothing at {path}.");
        }
        catch (InvalidDataException)
        {
            return CommandResult<FolderItem?>.Fail("bad_archive", $"There is a damaged zip on the way to {path}.");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem?>.Fail("permission_denied", $"Access denied: {path}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<FolderItem?>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<FolderItem?>.Fail("io_error", ex.Message);
        }
    }

    // ── LIST_DRIVES ──────────────────────────────────────────────────────
    // Every mounted volume, so the app is never pinned to the drive it
    // started on. A drive that isn't ready (empty card reader, disconnected
    // network share) is left out rather than offered and then failing.

    public CommandResult<DriveEntry[]> ListDrives(ListDrivesArgs args)
    {
        DriveInfo[] all;
        try
        {
            all = DriveInfo.GetDrives();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CommandResult<DriveEntry[]>.Fail("io_error", ex.Message);
        }

        List<DriveEntry> drives = [];
        foreach (DriveInfo drive in all)
        {
            try
            {
                if (!drive.IsReady)
                    continue;
                string root = drive.RootDirectory.FullName;
                string format = SafeDriveFormat(drive);
                if (OperatingSystem.IsLinux() && DriveFilter.IsSystemMount(root, format))
                    continue;
                string label = SafeVolumeLabel(drive, root);
                drives.Add(new DriveEntry(
                    root,
                    string.IsNullOrWhiteSpace(label) ? null : label,
                    drive.DriveType.ToString(),
                    drive.TotalSize,
                    drive.AvailableFreeSpace
                ));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Went away, or won't answer, between listing and asking.
            }
        }
        return CommandResult<DriveEntry[]>.Ok(DriveFilter.Deduplicate(drives));
    }

    /// <summary>
    /// Linux has no volume labels, and hands back the mount point instead —
    /// "Go to Drive /home (/home)" says nothing twice.
    /// </summary>
    private static string SafeVolumeLabel(DriveInfo drive, string root)
    {
        try
        {
            string label = drive.VolumeLabel;
            return PathCompare.PathMatches(label, root) ? string.Empty : label;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or DriveNotFoundException or PlatformNotSupportedException)
        {
            return string.Empty;
        }
    }

    /// <summary>A mount can refuse to name its filesystem; that alone is no reason to drop it.</summary>
    private static string SafeDriveFormat(DriveInfo drive)
    {
        try
        {
            return drive.DriveFormat;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DriveNotFoundException)
        {
            return string.Empty;
        }
    }

    // ── COPY_OUT_OF_ARCHIVE ──────────────────────────────────────────────
    // Nothing outside Rove can open a file that is still inside a zip, so
    // opening one means taking a copy out to a scratch folder and pointing
    // the program at that. The copy is read-only in the sense that matters:
    // whatever is written to it never finds its way back into the archive.

    public CommandResult<string?> CopyOutOfArchive(CopyOutOfArchiveArgs args)
    {
        if (!ArchivePath.TryParse(args.Path, out ArchivePath inside) || inside.IsRoot)
            return CommandResult<string?>.Fail("not_in_archive", $"{args.Path} is not inside a zip.");

        try
        {
            return CommandResult<string?>.Ok(ArchiveBrowser.CopyOut(inside));
        }
        catch (FileNotFoundException)
        {
            return CommandResult<string?>.Fail("not_found", $"{inside.Name} is not in that zip any more.");
        }
        catch (InvalidDataException)
        {
            return CommandResult<string?>.Fail(
                "bad_archive", $"{Path.GetFileName(inside.Archive)} is damaged, or is not really a zip file.");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<string?>.Fail("permission_denied", $"Access denied while reading {inside.Name}.");
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

    public Task<CommandResult<string?>> CopyOutOfArchiveAsync(CopyOutOfArchiveArgs args) =>
        Task.Run(() => CopyOutOfArchive(args));

    // ── LAUNCH_FILE ──────────────────────────────────────────────────────

    public CommandResult<string?> LaunchFile(LaunchFileArgs args)
    {
        if (ArchivePath.IsInside(args.Path))
        {
            return CommandResult<string?>.Fail(
                "in_archive", "A file inside a zip has to be taken out of it before anything can open it.");
        }

        string? error = FileOpener.Start(args.Path, out FileOpener.Opener? opener);
        opener?.Dispose();
        return error is null
            ? CommandResult<string?>.Ok(null)
            : CommandResult<string?>.Fail("launch_failed", error);
    }

    /// <summary>
    /// Same, and then waits to hear whether the opener actually took the
    /// file. Worth the wait: on Linux "nothing here opens this kind of file"
    /// arrives as an exit code, so without it the user presses Enter and
    /// nothing happens, with nothing said about why.
    /// </summary>
    public async Task<CommandResult<string?>> LaunchFileAsync(
        LaunchFileArgs args, CancellationToken ct = default
    )
    {
        string? error = FileOpener.Start(args.Path, out FileOpener.Opener? opener);
        if (error is not null)
        {
            opener?.Dispose();
            return CommandResult<string?>.Fail("launch_failed", error);
        }

        using (opener)
        {
            error = await FileOpener.WaitForRefusal(opener, args.Path, ct);
        }
        return error is null
            ? CommandResult<string?>.Ok(null)
            : CommandResult<string?>.Fail("launch_refused", error);
    }

    // ── Icons ────────────────────────────────────────────────────────────

    public async Task<CommandResult<byte[]?>> GetItemIcon(GetIconArgs args)
    {
        try
        {
            byte[]? icon = await _fetcher.GetIconAsync(args.Item, args.Size);
            return CommandResult<byte[]?>.Ok(icon);
        }
        catch (Exception)
        {
            return CommandResult<byte[]?>.Fail("icon_error", null);
        }
    }

    // ── RENAME_ITEM ──────────────────────────────────────────────────────
    // Rename keeps the item in its directory: it takes a bare new name and
    // never overwrites. A destination collision is an error, not a delete.

    public CommandResult<FolderItem?> RenameItem(RenameItemArgs args)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(LongPath.Display(args.Path)));
        if (parent is null)
            return CommandResult<FolderItem?>.Fail("invalid_path", "Cannot rename a root directory.");

        string? newPath = PathGuard.SafeCombine(parent, args.NewName, out string? reason);
        if (newPath is null)
            return CommandResult<FolderItem?>.Fail(reason!, $"Invalid name: {args.NewName}");

        string source = LongPath.ForIo(args.Path);
        string target = LongPath.ForIo(newPath);
        bool onlyCaseChanged = PathCompare.PathMatches(newPath, Path.GetFullPath(LongPath.Display(args.Path)));
        try
        {
            if (Directory.Exists(source))
            {
                if (!onlyCaseChanged && Exists(target))
                    return CommandResult<FolderItem?>.Fail("already_exists", $"Something named {args.NewName} already exists here.");
                Directory.Move(source, target);
                return CommandResult<FolderItem?>.Ok(FolderItem.From(new DirectoryInfo(target)));
            }
            if (File.Exists(source))
            {
                if (!onlyCaseChanged && Exists(target))
                    return CommandResult<FolderItem?>.Fail("already_exists", $"Something named {args.NewName} already exists here.");
                File.Move(source, target);
                return CommandResult<FolderItem?>.Ok(FolderItem.From(new FileInfo(target)));
            }
            return CommandResult<FolderItem?>.Fail("not_found", $"No such item: {args.Path}");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem?>.Fail("permission_denied", $"Access denied: {args.Path}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<FolderItem?>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<FolderItem?>.Fail("io_error", ex.Message);
        }
    }

    // ── MOVE_ITEMS ───────────────────────────────────────────────────────
    // Moves N items *into* a target directory, each keeping its own name.
    // Per-item results; a failure never touches anything already at the
    // destination.

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
            return OpResult.Success(sourcePath, null); // already there — nothing to do

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

    /// <summary>Directory.Move can't cross volumes; fall back to copy-then-delete,
    /// and only delete the source after the copy fully succeeded.</summary>
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

    // ── COPY_ITEMS ───────────────────────────────────────────────────────

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
        string source = LongPath.ForIo(sourcePath);
        string dest = LongPath.ForIo(destPath);

        try
        {
            ct.ThrowIfCancellationRequested();
            if (Directory.Exists(source))
            {
                if (PathGuard.IsSameOrDescendant(sourcePath, targetDir))
                    return OpResult.Failure(sourcePath, "invalid_target", "Cannot copy a folder into itself.");
                if (Exists(dest))
                    return OpResult.Failure(sourcePath, "already_exists", $"{name} already exists at the destination.");
                try
                {
                    CopyDirectory(source, dest, overwrite, "Copying", ticker, index, ct);
                }
                catch (OperationCanceledException)
                {
                    // The destination did not exist before this call, so the
                    // half-written tree is ours to clean up.
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
            // A junction or symlink is recreated as a link, not followed: following
            // one that points back into its own tree would copy forever, and this
            // is what most file explorers do with a link inside a copied folder.
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

    // ── EXTRACT_ARCHIVES ─────────────────────────────────────────────────
    // Each archive is unpacked into a new folder of its own beside it, so an
    // extract can never overwrite what is already in the directory.

    public CommandResult<OpResult[]> ExtractArchives(ExtractArchivesArgs args) =>
        ExtractArchives(args, null, CancellationToken.None);

    public Task<CommandResult<OpResult[]>> ExtractArchivesAsync(
        ExtractArchivesArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => ExtractArchives(args, progress, ct), CancellationToken.None);

    private static CommandResult<OpResult[]> ExtractArchives(
        ExtractArchivesArgs args, IProgress<FileOpProgress>? progress, CancellationToken ct
    ) => RunBatch("Extracting", args.Paths, progress, ct,
            (source, ticker, index) => ExtractOne(source, args.TargetDirectory, ticker, index, ct),
            () => Directory.Exists(LongPath.ForIo(args.TargetDirectory))
                ? null
                : CommandResult<OpResult[]>.Fail("not_found", $"Target directory does not exist: {args.TargetDirectory}"));

    private static OpResult ExtractOne(
        string archivePath, string targetDir, ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(archivePath)));
        string source = LongPath.ForIo(archivePath);

        if (!File.Exists(source))
            return OpResult.Failure(archivePath, "not_found", $"No such file: {archivePath}");
        if (!ArchiveService.IsArchive(archivePath))
            return OpResult.Failure(archivePath, "not_an_archive", $"{name} is not a zip file.");
        if (ArchiveService.FreeDestination(targetDir, archivePath) is not { } destination)
            return OpResult.Failure(archivePath, "already_exists", $"There is no free name left for {name} here.");

        try
        {
            ct.ThrowIfCancellationRequested();
            ArchiveService.Extract(archivePath, destination, "Extracting", ticker, index, ct);
            return OpResult.Success(archivePath, FolderItem.From(new DirectoryInfo(LongPath.ForIo(destination))));
        }
        catch (OperationCanceledException)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "cancelled", $"{name} was cancelled.");
        }
        catch (InvalidDataException)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "bad_archive", $"{name} is damaged, or is not really a zip file.");
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "permission_denied", $"Access denied while unpacking {name}.");
        }
        catch (PathTooLongException ex)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "io_error", ex.Message);
        }
    }

    // ── COMPRESS_ITEMS ───────────────────────────────────────────────────
    // The other direction: N items in, one zip out, beside them, under a name
    // nothing else in the folder has. Never overwrites, so it cannot lose
    // anything; a run that stops halfway takes its half-written zip with it.

    public CommandResult<OpResult[]> CompressItems(CompressItemsArgs args) =>
        CompressItems(args, null, CancellationToken.None);

    public Task<CommandResult<OpResult[]>> CompressItemsAsync(
        CompressItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => CompressItems(args, progress, ct), CancellationToken.None);

    private static CommandResult<OpResult[]> CompressItems(
        CompressItemsArgs args, IProgress<FileOpProgress>? progress, CancellationToken ct
    )
    {
        if (args.Paths.Length == 0)
            return CommandResult<OpResult[]>.Fail("nothing_to_do", "There is nothing here to compress.");
        if (!Directory.Exists(LongPath.ForIo(args.TargetDirectory)))
            return CommandResult<OpResult[]>.Fail("not_found", $"Target directory does not exist: {args.TargetDirectory}");

        string stem = ArchiveService.ArchiveStem(args.Paths, args.TargetDirectory);
        if (ArchiveService.FreeArchive(args.TargetDirectory, stem) is not { } destination)
            return CommandResult<OpResult[]>.Fail(
                "already_exists", $"There is no free name left for {stem}{ArchiveService.ArchiveExtension} here.");

        string name = Path.GetFileName(destination);
        ProgressTicker ticker = new(progress);
        ticker.Report("Compressing", 0, args.Paths.Length, name, important: true);

        try
        {
            ArchiveService.Compress(args.Paths, destination, "Compressing", ticker, ct);
            ticker.Report("Compressing", args.Paths.Length, args.Paths.Length, "", important: true);
            return Summarize([OpResult.Success(
                destination, FolderItem.From(new FileInfo(LongPath.ForIo(destination))))]);
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "cancelled", $"{name} was cancelled.")]);
        }
        catch (FileNotFoundException ex)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "not_found", ex.Message)]);
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "permission_denied", $"Access denied while making {name}.")]);
        }
        catch (PathTooLongException ex)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "path_too_long", ex.Message)]);
        }
        catch (IOException ex)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "io_error", ex.Message)]);
        }
    }

    // ── DELETE_ITEMS ─────────────────────────────────────────────────────
    // Everyday delete goes to the Recycle Bin, or the trash on Linux
    // (recoverable either way). Permanent
    // delete is a separate verb the UI must confirm.

    public CommandResult<OpResult[]> DeleteItems(DeleteItemsArgs args)
    {
        CommandResult<string?> trash = TrashService.MoveToTrash(args.Paths);
        if (!trash.IsOk)
            return CommandResult<OpResult[]>.Fail(trash.Reason, trash.Message);

        OpResult[] results = [.. args.Paths.Select(p => OpResult.Success(p))];
        return CommandResult<OpResult[]>.Ok(results);
    }

    /// <summary>
    /// The shell's Recycle Bin call is one atomic batch that can't be
    /// interrupted, so this reports that it started and then blocks off the
    /// UI thread — the window stays live even though Cancel can't reach it.
    /// </summary>
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

    // ── UNDO SUPPORT ─────────────────────────────────────────────────────
    // Taking an action back is the ordinary verbs run backwards, with two
    // exceptions that need their own rules: things that were never there
    // before must go away quietly, and things that were trashed have to come
    // back out of the Recycle Bin.

    /// <summary>
    /// Removes items an undo is taking back. Anything already gone is not a
    /// failure — the point is to end up with them not there. Prefers the
    /// trash, so an undo of an undo is still possible.
    /// </summary>
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

    /// <summary>
    /// Undo of "new file" / "new folder". It only removes what the create
    /// actually made: an empty file or an empty folder. Once there is
    /// something inside, the item is the user's, not ours to delete.
    /// </summary>
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

    // ── RESTORE_ITEMS ────────────────────────────────────────────────────

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

    /// <summary>
    /// Like the delete it reverses, this reports that it started and then
    /// gets on with it — it shows movement and nothing finer.
    /// </summary>
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

    // ── THE TRASH AS A PLACE ─────────────────────────────────────────────
    // On Linux the trash is an ordinary folder, so Rove can walk into it and
    // put things back from inside it. On Windows it is a shell folder that
    // cannot be listed, so this hands it to Explorer instead.

    /// <summary>The folder to open, or null when the trash was opened elsewhere.</summary>
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

    /// <summary>Puts items back out of the trash, named by where they sit in it.</summary>
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

    // ── CREATE_ITEM ──────────────────────────────────────────────────────
    // Validates the bare name (no traversal) and refuses to clobber an
    // existing item — File.Create would have truncated it.

    public CommandResult<FolderItem?> CreateItem(CreateItemArgs args)
    {
        string? path = PathGuard.SafeCombine(args.Directory, args.Name, out string? reason);
        if (path is null)
            return CommandResult<FolderItem?>.Fail(reason!, $"Invalid name: {args.Name}");

        string io = LongPath.ForIo(path);
        try
        {
            if (Exists(io))
                return CommandResult<FolderItem?>.Fail("already_exists", $"{args.Name} already exists.");

            if (args.IsDirectory)
            {
                DirectoryInfo dir = Directory.CreateDirectory(io);
                return CommandResult<FolderItem?>.Ok(FolderItem.From(dir));
            }
            using (new FileStream(io, FileMode.CreateNew)) { }
            return CommandResult<FolderItem?>.Ok(FolderItem.From(new FileInfo(io)));
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem?>.Fail("permission_denied", $"Access denied: {path}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<FolderItem?>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<FolderItem?>.Fail("io_error", ex.Message);
        }
    }

    // ── GET_METADATA ─────────────────────────────────────────────────────

    private const int MetadataWalkCap = 100_000;

    public Task<CommandResult<ItemMetadata?>> GetMetadataAsync(GetMetadataArgs args, CancellationToken ct = default) =>
        Task.Run(() => GetMetadata(args, ct), ct);

    public CommandResult<ItemMetadata?> GetMetadata(GetMetadataArgs args, CancellationToken ct = default)
    {
        string path = LongPath.ForIo(args.Path);
        try
        {
            bool isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path))
                return CommandResult<ItemMetadata?>.Fail("not_found", $"No such item: {args.Path}");

            FileSystemInfo info = isDirectory
                ? new DirectoryInfo(path)
                : new FileInfo(path);

            int? fileCount = null, dirCount = null;
            long? totalSize = null;
            bool truncated = false;

            if (isDirectory)
            {
                (fileCount, dirCount, totalSize, truncated) = WalkDirectoryStats(path, ct);
            }

            ItemMetadata meta = new(
                Name: info.Name,
                FullPath: LongPath.Display(info.FullName),
                IsDirectory: isDirectory,
                Extension: info is FileInfo fi ? fi.Extension : string.Empty,
                SizeBytes: info is FileInfo f ? f.Length : totalSize,
                CreatedUtc: info.CreationTimeUtc,
                ModifiedUtc: info.LastWriteTimeUtc,
                AccessedUtc: info.LastAccessTimeUtc,
                Attributes: info.Attributes,
                IsReadOnly: info.Attributes.HasFlag(FileAttributes.ReadOnly),
                IsHidden: info.Attributes.HasFlag(FileAttributes.Hidden),
                IsSystem: info.Attributes.HasFlag(FileAttributes.System),
                IsReparsePoint: info.Attributes.HasFlag(FileAttributes.ReparsePoint),
                LinkTarget: info.LinkTarget,
                UnixMode: OperatingSystem.IsWindows() ? null : info.UnixFileMode,
                FileCount: fileCount,
                DirectoryCount: dirCount,
                TotalSizeBytes: totalSize,
                Truncated: truncated
            );
            return CommandResult<ItemMetadata?>.Ok(meta);
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<ItemMetadata?>.Fail("permission_denied", $"Access denied: {args.Path}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<ItemMetadata?>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<ItemMetadata?>.Fail("io_error", ex.Message);
        }
    }

    private static (int files, int dirs, long size, bool truncated) WalkDirectoryStats(
        string path, CancellationToken ct
    )
    {
        EnumerationOptions options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        int files = 0, dirs = 0, visited = 0;
        long size = 0;
        bool truncated = false;

        DirectoryInfo dir = new(path);
        foreach (FileSystemInfo entry in dir.EnumerateFileSystemInfos("*", options))
        {
            if (ct.IsCancellationRequested || ++visited > MetadataWalkCap)
            {
                truncated = true;
                break;
            }
            if (entry is FileInfo file)
            {
                files++;
                size += file.Length;
            }
            else
            {
                dirs++;
            }
        }
        return (files, dirs, size, truncated);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// One item at a time, reporting before each and stopping early when the
    /// token is tripped — everything left over comes back as "cancelled" so
    /// the caller always gets a result per requested path.
    /// </summary>
    private static CommandResult<OpResult[]> RunBatch(
        string verb,
        string[] paths,
        IProgress<FileOpProgress>? progress,
        CancellationToken ct,
        Func<string, ProgressTicker, int, OpResult> runOne,
        Func<CommandResult<OpResult[]>?>? precondition
    )
    {
        if (precondition?.Invoke() is { } failed)
            return failed;

        ProgressTicker ticker = new(progress);
        OpResult[] results = new OpResult[paths.Length];
        bool cancelled = false;

        for (int i = 0; i < paths.Length; i++)
        {
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(paths[i])));
            if (cancelled || ct.IsCancellationRequested)
            {
                cancelled = true;
                results[i] = OpResult.Failure(paths[i], "cancelled", $"{name} was cancelled.");
                continue;
            }
            ticker.Report(verb, i, paths.Length, name, important: true);
            results[i] = runOne(paths[i], ticker, i);
            if (results[i].Reason == "cancelled")
                cancelled = true;
        }

        ticker.Report(verb, paths.Length, paths.Length, "", important: true);
        return Summarize(results);
    }

    private static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

    private static void TryDeleteTree(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort — a leftover partial copy beats throwing over the
            // cancellation the user actually asked for.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(LongPath.ForIo(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort, same as a half-written copy: what the user asked
            // for was to stop, not to be told about the leftovers.
        }
    }

    private static CommandResult<OpResult[]> Summarize(OpResult[] results)
    {
        int failed = results.Count(r => !r.Ok);
        if (failed == 0)
            return CommandResult<OpResult[]>.Ok(results);

        string message = string.Join("; ", results.Where(r => !r.Ok).Select(r => r.Message ?? r.Reason).Distinct());
        string reason = failed == results.Length ? results[0].Reason : "partial_failure";
        return CommandResult<OpResult[]>.Fail(reason, message, results);
    }
}

using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
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

    public CommandResult<FolderItem?> GetParent(GetParentArgs args)
    {
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
                return CommandResult<FolderItem?>.Ok(null);
            return CommandResult<FolderItem?>.Ok(FolderItem.From(parent));
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem?>.Fail("permission_denied", $"Access denied: {path}");
        }
    }

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
            }
        }
        return CommandResult<DriveEntry[]>.Ok(DriveFilter.Deduplicate(drives));
    }

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
}

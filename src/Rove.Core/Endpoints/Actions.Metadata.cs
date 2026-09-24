using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
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
        EnumerationOptions fastOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        EnumerationOptions flatOptions = new()
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        int files = 0, dirs = 0, visited = 0;
        long size = 0;
        bool truncated = false;

        void Walk(string start)
        {
            if (truncated || ct.IsCancellationRequested)
                return;

            int localFiles = 0, localDirs = 0, localVisited = 0;
            long localSize = 0;
            try
            {
                foreach (FileSystemInfo entry in new DirectoryInfo(start).EnumerateFileSystemInfos("*", fastOptions))
                {
                    if (ct.IsCancellationRequested || visited + ++localVisited > MetadataWalkCap)
                    {
                        truncated = true;
                        break;
                    }
                    try
                    {
                        if (entry is FileInfo file)
                        {
                            localFiles++;
                            localSize += file.Length;
                        }
                        else
                        {
                            localDirs++;
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                    }
                }
                files += localFiles;
                dirs += localDirs;
                size += localSize;
                visited += localVisited;
                return;
            }
            catch (Exception ex) when (ex is (UnauthorizedAccessException or IOException) && start != path)
            {
            }

            List<FileSystemInfo> children;
            try
            {
                children = [.. new DirectoryInfo(start).EnumerateFileSystemInfos("*", flatOptions)];
            }
            catch (Exception ex) when (ex is (UnauthorizedAccessException or IOException) && start != path)
            {
                return;
            }

            foreach (FileSystemInfo entry in children)
            {
                if (truncated || ct.IsCancellationRequested)
                    break;
                if (++visited > MetadataWalkCap)
                {
                    truncated = true;
                    break;
                }
                try
                {
                    if (entry is FileInfo file)
                    {
                        files++;
                        size += file.Length;
                    }
                    else
                    {
                        dirs++;
                        Walk(entry.FullName);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                }
            }
        }

        Walk(path);
        return (files, dirs, size, truncated);
    }
}

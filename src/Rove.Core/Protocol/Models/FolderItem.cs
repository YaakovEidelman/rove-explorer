using Rove.Core.Services;

namespace Rove.Core.Protocol;

public record FolderItem(
    string Name,
    string FullPath,
    FileAttributes Attributes,
    DateTime LastWriteTime,
    bool IsDirectory,
    long? Size,
    string Extension
)
{
    public static FolderItem From(FileSystemInfo info)
    {
        bool isDirectory = info.Attributes.HasFlag(FileAttributes.Directory);
        FileInfo? file = !isDirectory && info is FileInfo f ? f : null;

        return new FolderItem(
            info.Name,
            LongPath.Display(info.FullName),
            info.Attributes,
            info.LastWriteTime,
            isDirectory,
            file?.Length,
            file?.Extension ?? string.Empty
        );
    }

    public static FolderItem FromPath(string path)
    {
        string io = LongPath.ForIo(path);
        FileSystemInfo info = Directory.Exists(io)
            ? new DirectoryInfo(io)
            : new FileInfo(io);
        return From(info);
    }

    public static IEnumerable<FolderItem> From(IEnumerable<FileSystemInfo> info) => info.Select(From);
}

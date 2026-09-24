using System.Runtime.Versioning;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public class LinuxIconFetcher : IIconFetcher
{
    private static readonly string[] _directoryIcons = ["folder", "inode-directory", "default-folder"];
    private static readonly string[] _fileIcons = ["application-x-generic", "text-x-generic", "unknown"];

    private readonly Lazy<LinuxIconTheme> _theme = new(LinuxIconTheme.Load, true);
    private readonly Lazy<LinuxMimeDatabase> _mime = new(LinuxMimeDatabase.Load, true);

    public Task<byte[]?> GetIconAsync(FolderItem item, int size) => Task.Run(() => Fetch(item, size));

    private byte[]? Fetch(FolderItem item, int size)
    {
        LinuxIconTheme theme = _theme.Value;
        foreach (string name in CandidateNames(item))
        {
            if (theme.FindIconFile(name, size) is not { } file)
                continue;
            try
            {
                return File.ReadAllBytes(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return null;
    }

    private IEnumerable<string> CandidateNames(FolderItem item)
    {
        if (item.IsDirectory)
            return _directoryIcons;

        LinuxMimeDatabase mime = _mime.Value;
        if (mime.MimeForExtension(item.Extension) is not { } type)
            return _fileIcons;

        List<string> names = [];
        if (mime.ThemeIcon(type) is { } themed)
            names.Add(themed);
        names.Add(type.Replace('/', '-'));
        if (mime.GenericIcon(type) is { } generic)
            names.Add(generic);
        int slash = type.IndexOf('/');
        if (slash > 0)
            names.Add(type[..slash] + "-x-generic");
        names.AddRange(_fileIcons);
        return names;
    }
}

using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

public static class IconFetcherChooser
{
    public static IIconFetcher CreateForHost()
    {
        IIconFetcher fetcher;
        if (OperatingSystem.IsWindows())
            fetcher = new WindowsIconFetcher();
        else if (OperatingSystem.IsLinux())
            fetcher = new LinuxIconFetcher();
        else
            fetcher = new NoIconFetcher();
        return new CacheIconFetcher(fetcher);
    }
}

public interface IIconFetcher
{
    Task<byte[]?> GetIconAsync(FolderItem item, int size);
}

public class CacheIconFetcher : IIconFetcher
{
    private readonly IIconFetcher _inner;
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _cache = new();

    public CacheIconFetcher(IIconFetcher inner)
    {
        _inner = inner;
    }

    public Task<byte[]?> GetIconAsync(FolderItem item, int size)
    {
        string key = item.IsDirectory
            ? "<dir>"
            : string.IsNullOrEmpty(item.Extension) ? "<no-ext>" : item.Extension;
        key = $"{key}|{size}";
        return _cache.GetOrAdd(key, _ => FetchAndKeepOnlyIfGood(key, item, size));
    }

    private async Task<byte[]?> FetchAndKeepOnlyIfGood(string key, FolderItem item, int size)
    {
        try
        {
            byte[]? img = await _inner.GetIconAsync(item, size);
            if (img is null)
                _cache.TryRemove(key, out _);
            return img;
        }
        catch
        {
            _cache.TryRemove(key, out _);
            throw;
        }
    }
}

public class NoIconFetcher : IIconFetcher
{
    public Task<byte[]?> GetIconAsync(FolderItem item, int size)
    {
        return Task.FromResult((byte[]?)null);
    }
}

[SupportedOSPlatform("windows")]
public class WindowsIconFetcher : IIconFetcher
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbSizeFileInfo,
        uint uFlags
    );

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public Task<byte[]?> GetIconAsync(FolderItem item, int size) =>
        Task.Run(() => Fetch(item, size));

    private static readonly Lock _shellLock = new();

    private static byte[]? Fetch(FolderItem item, int size)
    {
        uint flags =
            SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (size > 16 ? SHGFI_LARGEICON : SHGFI_SMALLICON);

        uint attributes;
        string lookup;
        if (item.IsDirectory)
        {
            attributes = FILE_ATTRIBUTE_DIRECTORY;
            lookup = "folder";
        }
        else
        {
            attributes = FILE_ATTRIBUTE_NORMAL;
            lookup = string.IsNullOrEmpty(item.Extension) ? item.Name : item.Extension;
        }

        lock (_shellLock)
            return AskShell(lookup, attributes, flags);
    }

    private static byte[]? AskShell(string lookup, uint attributes, uint flags)
    {
        SHFILEINFO shinfo = new();
        IntPtr res = SHGetFileInfo(
            lookup,
            attributes,
            ref shinfo,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            flags
        );

        if (res == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            return null;

        try
        {
            using Icon icon = Icon.FromHandle(shinfo.hIcon);
            using Bitmap bmp = icon.ToBitmap();
            using MemoryStream ms = new();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
        finally
        {
            DestroyIcon(shinfo.hIcon);
        }
    }
}

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

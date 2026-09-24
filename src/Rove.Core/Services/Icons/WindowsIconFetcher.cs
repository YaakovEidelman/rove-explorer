using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

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

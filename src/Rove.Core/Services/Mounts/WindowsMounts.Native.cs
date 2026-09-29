using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Rove.Core.Services;

public sealed partial class WindowsMounts
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareReadWrite = 0x1 | 0x2;
    private const uint OpenExisting = 3;
    private const uint FsctlLockVolume = 0x00090018;
    private const uint FsctlDismountVolume = 0x00090020;
    private const uint IoctlStorageMediaRemoval = 0x002D4804;
    private const uint IoctlStorageEjectMedia = 0x002D4808;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int LockTries = 10;
    private const int DescriptorSize = 1024;

    private static (bool Usb, string? Model) Describe(string root)
    {
        using SafeFileHandle volume = Open(root, 0);
        if (volume.IsInvalid)
            return (false, null);
        byte[] query = new byte[12];
        byte[] descriptor = new byte[DescriptorSize];
        return DeviceIoControl(volume, IoctlStorageQueryProperty, query, query.Length,
                descriptor, descriptor.Length, out int returned, IntPtr.Zero)
            ? WindowsDriveList.ReadDescriptor(descriptor.AsSpan(0, returned))
            : (false, null);
    }

    private static string? Eject(string root)
    {
        string letter = root.TrimEnd('\\', '/');
        using SafeFileHandle volume = Open(root, GenericRead | GenericWrite);
        if (volume.IsInvalid)
            return $"Couldn't reach {letter}. It may already be unplugged.";
        if (!Lock(volume))
            return $"Something is still using {letter}. Close any files or windows from it and try again.";
        if (!Control(volume, FsctlDismountVolume, null))
            return $"Windows couldn't unmount {letter}.";
        Control(volume, IoctlStorageMediaRemoval, [0]);
        Control(volume, IoctlStorageEjectMedia, null);
        return null;
    }

    private static uint Serial(string root) =>
        GetVolumeInformationW(root, null, 0, out uint serial, out _, out _, null, 0) ? serial : 0;

    private static bool Lock(SafeFileHandle volume)
    {
        for (int tries = 0; tries < LockTries; tries++)
        {
            if (Control(volume, FsctlLockVolume, null))
                return true;
            Thread.Sleep(100);
        }
        return false;
    }

    private static bool Control(SafeFileHandle volume, uint code, byte[]? input) =>
        DeviceIoControl(volume, code, input, input?.Length ?? 0, null, 0, out _, IntPtr.Zero);

    private static SafeFileHandle Open(string root, uint access) =>
        CreateFileW($@"\\.\{root.TrimEnd('\\', '/')}", access, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device, uint code, byte[]? input, int inputSize,
        byte[]? output, int outputSize, out int returned, IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationW(
        string root, char[]? label, int labelSize, out uint serial, out uint maxComponent,
        out uint flags, char[]? fileSystem, int fileSystemSize);
}

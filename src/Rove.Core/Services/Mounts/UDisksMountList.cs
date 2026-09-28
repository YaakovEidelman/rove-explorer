using System.Globalization;

namespace Rove.Core.Services;

public static class UDisksMountList
{
    private static readonly string[] _userMountRoots = ["/media/", "/run/media/", "/mnt/"];
    private static readonly string[] _units = ["bytes", "kB", "MB", "GB", "TB", "PB"];

    public static MountEntry[] Build(UDisksObjects objects, string home)
    {
        List<MountEntry> entries = [];
        foreach (UDisksBlock block in objects.Blocks)
        {
            if (block.HintIgnore || block.CryptoBackingDevice is not null)
                continue;
            if (block.IsEncrypted)
            {
                if (objects.CleartextOf(block) is { } cleartext)
                {
                    if (!cleartext.HintIgnore && cleartext.HasFilesystem && Visible(cleartext, home))
                        entries.Add(Entry(objects, block, cleartext));
                }
                else
                {
                    entries.Add(Entry(objects, block, filesystem: null));
                }
            }
            else if (block.HasFilesystem && Visible(block, home))
            {
                entries.Add(Entry(objects, block, block));
            }
        }
        return [.. entries];
    }

    public static string SizeText(ulong bytes)
    {
        double size = bytes;
        int unit = 0;
        while (size >= 1000 && unit < _units.Length - 1)
        {
            size /= 1000;
            unit++;
        }
        string number = unit == 0 || size >= 10
            ? Math.Round(size).ToString(CultureInfo.InvariantCulture)
            : Math.Round(size, 1).ToString(CultureInfo.InvariantCulture);
        return $"{number} {_units[unit]}";
    }

    private static MountEntry Entry(UDisksObjects objects, UDisksBlock block, UDisksBlock? filesystem)
    {
        UDisksDrive? drive = objects.DriveOf(block);
        string? mountPoint = filesystem?.MountPoints.FirstOrDefault();
        return new MountEntry(
            Name(block, filesystem),
            drive?.Removable == true || drive?.Ejectable == true ? MountKind.Removable : MountKind.Disk,
            block.Device,
            ActivationUri: null,
            mountPoint is null ? null : new Uri(mountPoint).AbsoluteUri,
            mountPoint,
            CanMount: mountPoint is null,
            CanUnmount: mountPoint is not null,
            CanEject: drive is { Ejectable: true } or { CanPowerOff: true },
            VolumeId: block.Uuid.Length > 0 ? block.Uuid : null);
    }

    private static string Name(UDisksBlock block, UDisksBlock? filesystem)
    {
        if (filesystem is { Label.Length: > 0 })
            return filesystem.Label;
        if (block.Label.Length > 0)
            return block.Label;
        return block.IsEncrypted && filesystem is null
            ? $"{SizeText(block.Size)} Encrypted"
            : $"{SizeText(block.Size)} Volume";
    }

    private static bool Visible(UDisksBlock filesystem, string home) =>
        filesystem.MountPoints.Length == 0 || filesystem.MountPoints.Any(point => IsUserPlace(point, home));

    private static bool IsUserPlace(string mountPoint, string home)
    {
        if (_userMountRoots.Any(root => mountPoint.StartsWith(root, StringComparison.Ordinal)))
            return true;
        string homeRoot = Path.TrimEndingDirectorySeparator(home) + "/";
        return home.Length > 1 && mountPoint.StartsWith(homeRoot, StringComparison.Ordinal);
    }
}

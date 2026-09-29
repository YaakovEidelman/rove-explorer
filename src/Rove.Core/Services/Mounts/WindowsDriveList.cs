using System.Text;

namespace Rove.Core.Services;

public static class WindowsDriveList
{
    private const uint UsbBus = 7;
    private const int VendorOffsetAt = 12;
    private const int ProductOffsetAt = 16;
    private const int BusTypeAt = 28;

    public static MountEntry[] Build(IEnumerable<WindowsVolume> volumes) => [.. volumes.Select(Entry)];

    public static MountEntry Entry(WindowsVolume volume)
    {
        string letter = volume.Root.TrimEnd('\\', '/');
        return new MountEntry(
            Name(volume, letter),
            MountKind.Removable,
            letter,
            ActivationUri: null,
            new Uri(volume.Root).AbsoluteUri,
            volume.Root,
            CanMount: false,
            CanUnmount: false,
            CanEject: true,
            VolumeId: volume.Serial == 0 ? null : SerialText(volume.Serial),
            VolumeLabel: volume.Label.Length > 0 ? volume.Label : null);
    }

    public static string SerialText(uint serial) => $"{serial >> 16:X4}-{serial & 0xFFFF:X4}";

    public static (bool Usb, string? Model) ReadDescriptor(ReadOnlySpan<byte> descriptor)
    {
        if (descriptor.Length < BusTypeAt + 4)
            return (false, null);
        bool usb = BitConverter.ToUInt32(descriptor[BusTypeAt..]) == UsbBus;
        string model = string.Join(' ', new[]
        {
            Text(descriptor, BitConverter.ToUInt32(descriptor[VendorOffsetAt..])),
            Text(descriptor, BitConverter.ToUInt32(descriptor[ProductOffsetAt..])),
        }.Where(part => part.Length > 0));
        return (usb, model.Length > 0 ? model : null);
    }

    private static string Name(WindowsVolume volume, string letter)
    {
        if (volume.Model is { Length: > 0 } model)
            return volume.Label.Length > 0 ? $"{model} ({volume.Label}, {letter})" : $"{model} ({letter})";
        return volume.Label.Length > 0
            ? $"{volume.Label} ({letter})"
            : $"{UDisksMountList.SizeText((ulong)Math.Max(0, volume.Size))} Volume ({letter})";
    }

    private static string Text(ReadOnlySpan<byte> descriptor, uint offset)
    {
        if (offset == 0 || offset >= descriptor.Length)
            return "";
        ReadOnlySpan<byte> rest = descriptor[(int)offset..];
        int end = rest.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? rest : rest[..end]).Trim();
    }
}

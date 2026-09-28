using System.Text;
using Tmds.DBus.Protocol;

namespace Rove.Core.Services;

public sealed class UDisksObjects(IEnumerable<UDisksBlock> blocks, IEnumerable<UDisksDrive> drives)
{
    public const string BlockInterface = "org.freedesktop.UDisks2.Block";
    public const string DriveInterface = "org.freedesktop.UDisks2.Drive";
    public const string FilesystemInterface = "org.freedesktop.UDisks2.Filesystem";
    public const string EncryptedInterface = "org.freedesktop.UDisks2.Encrypted";

    private static readonly string[] _removableBuses = ["usb", "sdio", "ieee1394"];

    public IReadOnlyList<UDisksBlock> Blocks { get; } = [.. blocks.OrderBy(b => b.Device, StringComparer.Ordinal)];

    public IReadOnlyList<UDisksDrive> Drives { get; } = [.. drives];

    public UDisksBlock? BlockOf(string device) => Blocks.FirstOrDefault(b => b.Device == device);

    public UDisksBlock? CleartextOf(UDisksBlock encrypted) =>
        Blocks.FirstOrDefault(b => b.CryptoBackingDevice == encrypted.Path);

    public UDisksDrive? DriveOf(UDisksBlock block)
    {
        string? path = block.Drive
            ?? Blocks.FirstOrDefault(b => b.Path == block.CryptoBackingDevice)?.Drive;
        return path is null ? null : Drives.FirstOrDefault(d => d.Path == path);
    }

    public static UDisksObjects Read(Dictionary<string, Dictionary<string, Dictionary<string, VariantValue>>> objects)
    {
        List<UDisksBlock> blocks = [];
        List<UDisksDrive> drives = [];
        foreach ((string path, Dictionary<string, Dictionary<string, VariantValue>> interfaces) in objects)
        {
            if (interfaces.TryGetValue(BlockInterface, out Dictionary<string, VariantValue>? block))
                blocks.Add(ReadBlock(path, block, interfaces));
            if (interfaces.TryGetValue(DriveInterface, out Dictionary<string, VariantValue>? drive))
                drives.Add(ReadDrive(path, drive));
        }
        return new UDisksObjects(blocks, drives);
    }

    private static UDisksBlock ReadBlock(
        string path,
        Dictionary<string, VariantValue> block,
        Dictionary<string, Dictionary<string, VariantValue>> interfaces)
    {
        string label = Text(block, "HintName") is { Length: > 0 } hint ? hint : Text(block, "IdLabel");
        string[] mountPoints = interfaces.TryGetValue(FilesystemInterface, out Dictionary<string, VariantValue>? fs)
            && fs.TryGetValue("MountPoints", out VariantValue points)
                ? [.. Enumerable.Range(0, points.Count).Select(i => Bytes(points.GetItem(i))).Where(p => p.Length > 0)]
                : [];

        return new UDisksBlock(
            path,
            block.TryGetValue("Device", out VariantValue device) ? Bytes(device) : "",
            label,
            block.TryGetValue("Size", out VariantValue size) ? size.GetUInt64() : 0,
            ObjectPath(block, "Drive"),
            Flag(block, "HintIgnore"),
            fs is not null,
            mountPoints,
            interfaces.ContainsKey(EncryptedInterface),
            ObjectPath(block, "CryptoBackingDevice"));
    }

    private static UDisksDrive ReadDrive(string path, Dictionary<string, VariantValue> drive) =>
        new(
            path,
            Flag(drive, "Removable") || Flag(drive, "MediaRemovable")
                || _removableBuses.Contains(Text(drive, "ConnectionBus"), StringComparer.Ordinal),
            Flag(drive, "Ejectable"),
            Flag(drive, "CanPowerOff"));

    private static string Bytes(VariantValue value) =>
        Encoding.UTF8.GetString(value.GetArray<byte>()).TrimEnd('\0');

    private static string Text(Dictionary<string, VariantValue> props, string key) =>
        props.TryGetValue(key, out VariantValue value) ? value.GetString() : "";

    private static bool Flag(Dictionary<string, VariantValue> props, string key) =>
        props.TryGetValue(key, out VariantValue value) && value.GetBool();

    private static string? ObjectPath(Dictionary<string, VariantValue> props, string key) =>
        props.TryGetValue(key, out VariantValue value) && value.GetObjectPathAsString() is { } path && path != "/"
            ? path
            : null;
}

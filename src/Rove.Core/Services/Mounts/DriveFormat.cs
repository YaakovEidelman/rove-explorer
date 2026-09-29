namespace Rove.Core.Services;

public sealed record DriveFormat(string Type, string Name, string Note, int NameLimit)
{
    public static readonly DriveFormat[] All =
    [
        new("exfat", "exFAT", "works on Windows, Mac, Linux and most devices", 11),
        new("vfat", "FAT32", "for older devices, files up to 4 GB", 11),
        new("ntfs", "NTFS", "for Windows", 32),
        new("ext4", "ext4", "Linux only", 16),
    ];

    public static string PartitionType(string type) => type switch
    {
        "vfat" => "0x0c",
        "ext4" => "0x83",
        _ => "0x07",
    };
}

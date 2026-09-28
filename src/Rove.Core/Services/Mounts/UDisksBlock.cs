namespace Rove.Core.Services;

public sealed record UDisksBlock(
    string Path,
    string Device,
    string Label,
    ulong Size,
    string? Drive,
    bool HintIgnore,
    bool HasFilesystem,
    string[] MountPoints,
    bool IsEncrypted,
    string? CryptoBackingDevice,
    string Uuid = ""
);

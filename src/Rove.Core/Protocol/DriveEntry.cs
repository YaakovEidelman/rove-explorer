namespace Rove.Core.Protocol;

/// <summary>
/// One mounted volume the user can jump to. RootPath is a real directory
/// ("C:\\", "D:\\", "/" or a mount point), so everything downstream treats a
/// drive as an ordinary folder.
/// </summary>
public record DriveEntry(
    string RootPath,
    string? Label,
    string DriveType,
    long? TotalSizeBytes,
    long? FreeBytes
);

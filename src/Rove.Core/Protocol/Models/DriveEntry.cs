namespace Rove.Core.Protocol;

public record DriveEntry(
    string RootPath,
    string? Label,
    string DriveType,
    long? TotalSizeBytes,
    long? FreeBytes
);

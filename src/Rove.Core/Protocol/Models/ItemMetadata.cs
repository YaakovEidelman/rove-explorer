namespace Rove.Core.Protocol;

public record ItemMetadata(
    string Name,
    string FullPath,
    bool IsDirectory,
    string Extension,
    long? SizeBytes,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    DateTime AccessedUtc,
    FileAttributes Attributes,
    bool IsReadOnly,
    bool IsHidden,
    bool IsSystem,
    bool IsReparsePoint,
    string? LinkTarget,
    UnixFileMode? UnixMode,
    int? FileCount,
    int? DirectoryCount,
    long? TotalSizeBytes,
    bool Truncated
);

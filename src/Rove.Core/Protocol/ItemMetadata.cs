namespace Rove.Core.Protocol;

/// <summary>
/// Explicit details for one item (GET_METADATA). Directory statistics are
/// computed by walking the tree and may be capped; Truncated says whether
/// the walk stopped early (hit the cap or was cancelled).
/// </summary>
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
    // Null on Windows, which has no equivalent permission model.
    UnixFileMode? UnixMode,
    // Directory-only statistics (null for files):
    int? FileCount,
    int? DirectoryCount,
    long? TotalSizeBytes,
    bool Truncated
);

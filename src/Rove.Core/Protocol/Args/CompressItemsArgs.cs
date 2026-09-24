using Rove.Core.Services;

namespace Rove.Core.Protocol;

public record CompressItemsArgs(string[] Paths, string TargetDirectory, ArchiveFormat Format = ArchiveFormat.Zip);

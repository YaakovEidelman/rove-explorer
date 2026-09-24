namespace Rove.Core.Protocol;

public record CopyItemsArgs(string[] Paths, string TargetDirectory, bool Overwrite);

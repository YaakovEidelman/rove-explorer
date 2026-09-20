namespace Rove.Core.Protocol;

public record MoveItemsArgs(string[] Paths, string TargetDirectory, bool Overwrite);

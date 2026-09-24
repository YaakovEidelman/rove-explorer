namespace Rove.Core.Protocol;

public record OpResult(string Path, bool Ok, string Reason, string? Message, FolderItem? Item)
{
    public static OpResult Success(string path, FolderItem? item = null) => new(path, true, "", null, item);
    public static OpResult Failure(string path, string reason, string? message) => new(path, false, reason, message, null);
}

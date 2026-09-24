namespace Rove.UI.Services;

public interface IRoveClipboardService
{
    Task CopyTextAsync(string text);

    Task SetFilesAsync(IReadOnlyList<string> paths, ClipboardOp op, Guid token);

    Task<(IReadOnlyList<string> Paths, ClipboardOp Op, Guid? Token)?> TryGetFilesAsync();

    Task ClearAsync();
}

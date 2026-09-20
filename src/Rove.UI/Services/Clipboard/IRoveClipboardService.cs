namespace Rove.UI.Services;

public interface IRoveClipboardService
{
    Task CopyTextAsync(string text);

    Task SetFilesAsync(IReadOnlyList<string> paths, ClipboardOp op);

    Task<(IReadOnlyList<string> Paths, ClipboardOp Op)?> TryGetFilesAsync();
}

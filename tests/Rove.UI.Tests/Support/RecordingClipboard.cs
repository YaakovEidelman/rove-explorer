using Rove.UI.Services;

namespace Rove.UI.Tests;

internal sealed class RecordingClipboard : IRoveClipboardService
{
    public string? LastText { get; private set; }

    public Task CopyTextAsync(string text)
    {
        LastText = text;
        return Task.CompletedTask;
    }

    public Task SetFilesAsync(IReadOnlyList<string> paths, ClipboardOp op) => Task.CompletedTask;

    public Task<(IReadOnlyList<string> Paths, ClipboardOp Op)?> TryGetFilesAsync() =>
        Task.FromResult<(IReadOnlyList<string> Paths, ClipboardOp Op)?>(null);
}

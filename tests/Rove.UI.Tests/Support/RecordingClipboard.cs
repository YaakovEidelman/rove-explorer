using Rove.UI.Services;

namespace Rove.UI.Tests;

internal sealed class RecordingClipboard : IRoveClipboardService
{
    public string? LastText { get; private set; }

    private (IReadOnlyList<string> Paths, ClipboardOp Op, Guid Token)? _files;

    public Task CopyTextAsync(string text)
    {
        LastText = text;
        return Task.CompletedTask;
    }

    public Task SetFilesAsync(IReadOnlyList<string> paths, ClipboardOp op, Guid token)
    {
        _files = (paths, op, token);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<string> Paths, ClipboardOp Op, Guid? Token)?> TryGetFilesAsync()
    {
        if (_files is not { } files)
            return Task.FromResult<(IReadOnlyList<string> Paths, ClipboardOp Op, Guid? Token)?>(null);

        // Real OS clipboards round-trip paths through their own storage APIs, which can
        // hand back a differently-formatted (but equivalent) string, e.g. a trailing
        // separator on folders. Reproduce that here so tests catch identity checks that
        // rely on exact path equality instead of the clipboard token.
        IReadOnlyList<string> roundTripped =
            [.. files.Paths.Select(p => Directory.Exists(p) ? p + Path.DirectorySeparatorChar : p)];
        return Task.FromResult<(IReadOnlyList<string> Paths, ClipboardOp Op, Guid? Token)?>(
            (roundTripped, files.Op, files.Token));
    }

    public Task ClearAsync()
    {
        _files = null;
        return Task.CompletedTask;
    }
}

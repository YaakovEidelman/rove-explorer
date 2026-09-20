using Avalonia.Media;
using Rove.UI.Services;

namespace Rove.UI.Tests;

internal sealed class NullPreviewLoader : IImagePreviewLoader
{
    public bool CanPreview(string extension) => false;

    public Task<IImage?> LoadAsync(string path, int width, CancellationToken ct) =>
        Task.FromResult<IImage?>(null);
}

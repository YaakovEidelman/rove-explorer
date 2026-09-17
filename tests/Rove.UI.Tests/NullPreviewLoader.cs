using Avalonia.Media;
using Rove.UI.Services;

namespace Rove.UI.Tests;

/// <summary>Draws nothing: the preview pane is not what these tests are about.</summary>
internal sealed class NullPreviewLoader : IImagePreviewLoader
{
    public bool CanPreview(string extension) => false;

    public Task<IImage?> LoadAsync(string path, int width, CancellationToken ct) =>
        Task.FromResult<IImage?>(null);
}

using Avalonia.Media;

namespace Rove.UI.Services;

public interface IImagePreviewLoader
{
    bool CanPreview(string extension);

    Task<IImage?> LoadAsync(string path, int width, CancellationToken ct);
}

using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Rove.UI.Services;

public class ImagePreviewLoader : IImagePreviewLoader
{
    private const long NativeSizeBytes = 512 * 1024;

    private static readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".webp", ".ico", ".tif", ".tiff",
    };

    public bool CanPreview(string extension) =>
        !string.IsNullOrEmpty(extension) && _extensions.Contains(extension);

    public Task<IImage?> LoadAsync(string path, int width, CancellationToken ct) =>
        Task.Run<IImage?>(() =>
        {
            try
            {
                using FileStream file = File.OpenRead(path);
                ct.ThrowIfCancellationRequested();
                return file.Length <= NativeSizeBytes
                    ? new Bitmap(file)
                    : Bitmap.DecodeToWidth(file, width, BitmapInterpolationMode.MediumQuality);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }, ct);
}

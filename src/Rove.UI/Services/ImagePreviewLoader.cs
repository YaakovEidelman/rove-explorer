using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rove.UI.Services;

public interface IImagePreviewLoader
{
    /// <summary>True when the extension is one the preview pane can draw.</summary>
    bool CanPreview(string extension);

    /// <summary>
    /// Decodes the image at <paramref name="path"/>, no wider than
    /// <paramref name="width"/>, or null when it isn't readable as one.
    /// Returns null rather than throwing: a preview that can't be drawn is
    /// not an error worth interrupting the user for.
    /// </summary>
    Task<IImage?> LoadAsync(string path, int width, CancellationToken ct);
}

/// <summary>
/// Decodes image files for the preview pane, downscaled to the pane's width
/// on a background thread so a 40-megapixel photo neither stalls the UI nor
/// sits in memory at full size.
/// </summary>
public class ImagePreviewLoader : IImagePreviewLoader
{
    /// <summary>
    /// Under this, decode at native size — scaling a small image up to the
    /// pane width only makes it blurry. Above it, decode straight to the pane
    /// width so a huge photo never lands in memory whole.
    /// </summary>
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

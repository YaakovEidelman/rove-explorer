using Avalonia.Media;
using Rove.Core.Protocol;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class IconCacheTests : HeadlessTest
{
    [Fact]
    public async Task ImageFilesGetARealThumbnailInsteadOfTheGenericIcon()
    {
        string path = CopyAssetToTemp("rove-32.png");
        try
        {
            IconCache cache = new(_ => Task.FromResult(CommandResult<byte[]?>.Fail("should_not_be_called", null)),
                new ImagePreviewLoader());

            IImage? icon = await OnUiThread(() => cache.GetIconAsync(FolderItem.FromPath(path), 48));

            Assert.NotNull(icon);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DifferentImageFilesWithTheSameExtensionGetIndependentThumbnails()
    {
        string pathA = CopyAssetToTemp("rove-16.png");
        string pathB = CopyAssetToTemp("rove-32.png");
        try
        {
            IconCache cache = new(_ => Task.FromResult(CommandResult<byte[]?>.Fail("should_not_be_called", null)),
                new ImagePreviewLoader());

            (IImage? iconA, IImage? iconB) = await OnUiThread(async () =>
            {
                IImage? a = await cache.GetIconAsync(FolderItem.FromPath(pathA), 48);
                IImage? b = await cache.GetIconAsync(FolderItem.FromPath(pathB), 48);
                return (a, b);
            });

            Assert.NotNull(iconA);
            Assert.NotNull(iconB);
            Assert.NotSame(iconA, iconB);
        }
        finally
        {
            File.Delete(pathA);
            File.Delete(pathB);
        }
    }

    [Fact]
    public async Task NonImageFilesStillUseTheGenericIconFetch()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        File.WriteAllText(path, "not an image");
        byte[] generic = File.ReadAllBytes(AssetPath("rove-16.png"));
        try
        {
            IconCache cache = new(_ => Task.FromResult(CommandResult<byte[]?>.Ok(generic)), new ImagePreviewLoader());

            IImage? icon = await OnUiThread(() => cache.GetIconAsync(FolderItem.FromPath(path), 16));

            Assert.NotNull(icon);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CopyAssetToTemp(string assetFileName)
    {
        string destination = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        File.Copy(AssetPath(assetFileName), destination);
        return destination;
    }

    private static string AssetPath(string assetFileName)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Rove.slnx")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Could not find repo root (Rove.slnx) above the test assembly.");
        return Path.Combine(dir.FullName, "src", "Rove.UI", "Assets", "png", assetFileName);
    }
}

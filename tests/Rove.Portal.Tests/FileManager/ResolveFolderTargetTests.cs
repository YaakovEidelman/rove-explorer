using Xunit;

namespace Rove.Portal.Tests;

public class ResolveFolderTargetTests
{
    [Fact]
    public void AnExistingDirectoryIsUsedDirectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "rove-filemanager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            Assert.Equal(tempDir, FileManagerHandler.ResolveFolderTarget(new Uri(tempDir).AbsoluteUri));
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void ANonExistentPathFallsBackToItsParent()
    {
        Assert.Equal("/home/user/Downloads", FileManagerHandler.ResolveFolderTarget("file:///home/user/Downloads/does-not-exist"));
    }
}

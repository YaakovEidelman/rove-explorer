using Xunit;

namespace Rove.Portal.Tests;

public class ToLocalPathTests
{
    [Fact]
    public void AFileUriDecodesToItsPath()
    {
        Assert.Equal("/home/user/Downloads/report.pdf", FileManagerHandler.ToLocalPath("file:///home/user/Downloads/report.pdf"));
    }

    [Fact]
    public void EscapedSpacesDecodeBackToSpaces()
    {
        Assert.Equal("/home/user/My Report.pdf", FileManagerHandler.ToLocalPath("file:///home/user/My%20Report.pdf"));
    }

    [Fact]
    public void ANonFileUriIsRejected()
    {
        Assert.Null(FileManagerHandler.ToLocalPath("http://example.com/report.pdf"));
    }
}

public class ResolveItemTargetTests
{
    [Fact]
    public void AFilesTargetIsItsContainingFolder()
    {
        Assert.Equal("/home/user/Downloads", FileManagerHandler.ResolveItemTarget("file:///home/user/Downloads/report.pdf"));
    }

    [Fact]
    public void ARootLevelFileResolvesToTheRootFolder()
    {
        Assert.Equal("/", FileManagerHandler.ResolveItemTarget("file:///report.pdf"));
    }

    [Fact]
    public void ANonFileUriResolvesToNothing()
    {
        Assert.Null(FileManagerHandler.ResolveItemTarget("http://example.com/report.pdf"));
    }
}

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

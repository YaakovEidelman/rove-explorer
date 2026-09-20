using Xunit;

namespace Rove.Portal.Tests;

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

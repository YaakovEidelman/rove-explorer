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

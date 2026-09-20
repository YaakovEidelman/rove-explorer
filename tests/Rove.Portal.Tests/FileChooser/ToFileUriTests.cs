using Xunit;

namespace Rove.Portal.Tests;

public class ToFileUriTests
{
    [Fact]
    public void APathBecomesAFileUri()
    {
        Assert.Equal("file:///home/user/report.pdf", FileChooserHandler.ToFileUri("/home/user/report.pdf"));
    }

    [Fact]
    public void SpacesAreEscaped()
    {
        Assert.Equal("file:///home/user/My%20Report.pdf", FileChooserHandler.ToFileUri("/home/user/My Report.pdf"));
    }
}

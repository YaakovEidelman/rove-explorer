using System.Text;
using Xunit;

namespace Rove.Portal.Tests;

public class DecodePathTests
{
    [Fact]
    public void ANullTerminatedByteArrayDecodesToItsPath()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("/home/user/Documents\0");

        Assert.Equal("/home/user/Documents", FileChooserHandler.DecodePath(bytes));
    }

    [Fact]
    public void ABareByteArrayWithNoTrailingNullStillDecodes()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("/home/user/Documents");

        Assert.Equal("/home/user/Documents", FileChooserHandler.DecodePath(bytes));
    }
}

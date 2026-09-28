using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class MountAddressTests
{
    [Theory]
    [InlineData("sftp://host/home/me")]
    [InlineData("smb://nas/share")]
    [InlineData("  ftp://ftp.gnu.org/ ")]
    [InlineData("mtp://Pixel/")]
    public void ServerAddressesLookRemote(string text)
    {
        Assert.True(MountAddress.LooksRemote(text));
    }

    [Theory]
    [InlineData("/home/me")]
    [InlineData("file:///home/me")]
    [InlineData("~/Downloads")]
    [InlineData("C:\\Users")]
    [InlineData("sftp://")]
    public void PathsDoNotLookRemote(string text)
    {
        Assert.False(MountAddress.LooksRemote(text));
    }

    [Fact]
    public void AUserInTheAddressIsNoticed()
    {
        Assert.True(MountAddress.HasUser("sftp://me@host/"));
        Assert.False(MountAddress.HasUser("sftp://host/"));
    }
}

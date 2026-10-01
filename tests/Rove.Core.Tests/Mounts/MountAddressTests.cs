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

    [Theory]
    [InlineData("sftp://host/srv", "me", "sftp://me@host/srv")]
    [InlineData("sftp://you@host/srv", "me", "sftp://you@host/srv")]
    [InlineData("smb://nas/share", "", "smb://nas/share")]
    [InlineData("sftp://host", "a b", "sftp://a%20b@host")]
    public void AUserIsAddedOnlyWhenTheAddressHasNone(string address, string user, string expected)
    {
        Assert.Equal(expected, MountAddress.WithUser(address, user));
    }

    [Fact]
    public void TheUserIsReadBackOut()
    {
        Assert.Equal("me", MountAddress.UserOf("sftp://me@host/"));
        Assert.Null(MountAddress.UserOf("sftp://host/"));
    }

    [Theory]
    [InlineData("sftp://me@host/srv", "me@host")]
    [InlineData("smb://nas/share", "nas")]
    public void AShortNameIsTheUserAndHost(string address, string expected)
    {
        Assert.Equal(expected, MountAddress.ShortName(address));
    }

    [Theory]
    [InlineData("sftp://me@host/srv", "sftp://me@host/", true)]
    [InlineData("sftp://host/srv", "sftp://me@HOST/", true)]
    [InlineData("sftp://me@host/", "sftp://you@host/", false)]
    [InlineData("sftp://host/", "smb://host/", false)]
    [InlineData("sftp://host:2222/", "sftp://host/", false)]
    public void TheSameServerMatchesAcrossPaths(string first, string second, bool same)
    {
        Assert.Equal(same, MountAddress.SameServer(first, second));
    }
}

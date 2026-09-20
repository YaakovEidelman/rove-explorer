using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PortalInstallAskedTests
{
    [Fact]
    public void NothingAskedYetReturnsFalse()
    {
        using PortalHome home = new();

        Assert.False(PortalInstall.HasAskedAboutDefault(home.AskedPath));
    }

    [Fact]
    public void MarkingAskedIsRemembered()
    {
        using PortalHome home = new();

        PortalInstall.MarkAskedAboutDefault(home.AskedPath);

        Assert.True(PortalInstall.HasAskedAboutDefault(home.AskedPath));
    }
}

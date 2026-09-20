using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PortalInstallDisableTests
{
    [Fact]
    public void DisablingAFreshClaimDeletesTheConfigAndWithdraws()
    {
        using PortalHome home = new();
        PortalInstall.Enable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove");

        bool disabled = PortalInstall.Disable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal");

        Assert.True(disabled);
        Assert.False(File.Exists(home.ConfigPath));
        Assert.False(File.Exists(home.StatePath));
        Assert.False(File.Exists(PortalInstall.PortalFilePath(home.DataHome)));
        Assert.False(File.Exists(PortalInstall.ServiceFilePath(home.DataHome)));
    }

    [Fact]
    public void WithNothingToRevertItSaysSo()
    {
        using PortalHome home = new();

        Assert.False(PortalInstall.RevertBackend(home.ConfigPath, home.StatePath));
    }

    [Fact]
    public void DisablingCallsRestartOnlyWhenItActuallyReverts()
    {
        using PortalHome home = new();
        int restarts = 0;

        bool disabledWithNothingClaimed = PortalInstall.Disable(
            home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", () => restarts++);
        Assert.False(disabledWithNothingClaimed);
        Assert.Equal(0, restarts);

        PortalInstall.Enable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove");
        bool disabled = PortalInstall.Disable(
            home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", () => restarts++);
        Assert.True(disabled);
        Assert.Equal(1, restarts);
    }

    [Fact]
    public void RevertingUsesTheConfigPathRecordedAtClaimTimeNotTheCurrentOne()
    {
        using PortalHome home = new();
        PortalInstall.Enable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove");

        string differentPath = Path.Combine(home.ConfigHome, "xdg-desktop-portal", "gnome-portals.conf");

        bool reverted = PortalInstall.RevertBackend(differentPath, home.StatePath);

        Assert.True(reverted);
        Assert.False(File.Exists(home.ConfigPath));
        Assert.False(File.Exists(differentPath));
    }
}

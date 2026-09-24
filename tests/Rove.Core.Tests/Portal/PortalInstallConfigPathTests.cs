using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PortalInstallConfigPathTests
{
    [Fact]
    public void WithNoDesktopItUsesTheGenericFile()
    {
        using PortalHome home = new();

        Assert.Equal(
            Path.Combine(home.ConfigHome, "xdg-desktop-portal", "portals.conf"),
            PortalInstall.ConfigPath(home.ConfigHome, currentDesktop: null));
    }

    [Fact]
    public void WithADesktopItUsesTheDesktopSpecificFile()
    {
        using PortalHome home = new();

        Assert.Equal(
            Path.Combine(home.ConfigHome, "xdg-desktop-portal", "hyprland-portals.conf"),
            PortalInstall.ConfigPath(home.ConfigHome, currentDesktop: "Hyprland"));
    }

    [Fact]
    public void WithMultipleColonSeparatedDesktopsItUsesTheFirst()
    {
        using PortalHome home = new();

        Assert.Equal(
            Path.Combine(home.ConfigHome, "xdg-desktop-portal", "ubuntu-portals.conf"),
            PortalInstall.ConfigPath(home.ConfigHome, currentDesktop: "ubuntu:GNOME"));
    }
}

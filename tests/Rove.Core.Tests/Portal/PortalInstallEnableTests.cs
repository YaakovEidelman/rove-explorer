using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PortalInstallEnableTests
{
    [Fact]
    public void EnablingWithNothingThereClaimsItAndAdvertises()
    {
        using PortalHome home = new();

        bool enabled = PortalInstall.Enable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove");

        Assert.True(enabled);
        Assert.Equal(PortalStatus.OwnedByRove, PortalInstall.CurrentStatus(home.ConfigPath, home.StatePath));
        Assert.True(File.Exists(PortalInstall.PortalFilePath(home.DataHome)));
        Assert.True(File.Exists(PortalInstall.ServiceFilePath(home.DataHome)));
    }

    [Fact]
    public void EnablingOverSomeoneElsesConfigBacksItUpAndTakesOver()
    {
        using PortalHome home = new();
        Directory.CreateDirectory(Path.GetDirectoryName(home.ConfigPath)!);
        File.WriteAllText(home.ConfigPath, "[preferred]\ndefault=gtk\n");

        bool enabled = PortalInstall.Enable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove");

        Assert.True(enabled);
        Assert.Contains("FileChooser=rove", File.ReadAllText(home.ConfigPath), StringComparison.Ordinal);

        PortalInstall.Disable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal");
        Assert.Equal("[preferred]\ndefault=gtk\n", File.ReadAllText(home.ConfigPath));
    }

    [Fact]
    public void EnablingCallsRestartOnlyWhenItActuallyClaims()
    {
        using PortalHome home = new();
        int restarts = 0;

        bool enabled = PortalInstall.Enable(
            home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove", () => restarts++);
        Assert.True(enabled);
        Assert.Equal(1, restarts);

        bool enabledAgain = PortalInstall.Enable(
            home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove", () => restarts++);
        Assert.False(enabledAgain);
        Assert.Equal(1, restarts);
    }

    [Fact]
    public void EnablingWhatsAlreadyOursDoesNothingFurther()
    {
        using PortalHome home = new();
        PortalInstall.Enable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove");

        bool enabledAgain = PortalInstall.Enable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove");

        Assert.False(enabledAgain);
    }
}

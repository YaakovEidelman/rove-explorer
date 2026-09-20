using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PortalInstallStatusTests
{
    [Fact]
    public void NothingAtAllMeansNotInstalled()
    {
        using PortalHome home = new();

        Assert.Equal(PortalStatus.NotInstalled, PortalInstall.CurrentStatus(home.ConfigPath, home.StatePath));
    }

    [Fact]
    public void AConfigRoveWroteMeansOwnedByRove()
    {
        using PortalHome home = new();
        PortalInstall.Enable(home.DataHome, home.ConfigPath, home.StatePath, "/opt/rove/rove-portal", "rove");

        Assert.Equal(PortalStatus.OwnedByRove, PortalInstall.CurrentStatus(home.ConfigPath, home.StatePath));
    }

    [Fact]
    public void AConfigSomeoneElseWroteMeansOwnedByOther()
    {
        using PortalHome home = new();
        Directory.CreateDirectory(Path.GetDirectoryName(home.ConfigPath)!);
        File.WriteAllText(home.ConfigPath, "[preferred]\ndefault=gtk\n");

        Assert.Equal(PortalStatus.OwnedByOther, PortalInstall.CurrentStatus(home.ConfigPath, home.StatePath));
    }
}

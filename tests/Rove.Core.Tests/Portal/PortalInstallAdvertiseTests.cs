using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PortalInstallAdvertiseTests
{
    [Fact]
    public void AdvertisingWritesThePortalAndServiceFiles()
    {
        using PortalHome home = new();

        PortalInstall.Advertise(home.DataHome, "/opt/rove/rove-portal");

        string portalFile = File.ReadAllText(PortalInstall.PortalFilePath(home.DataHome));
        Assert.Contains($"DBusName={PortalFiles.BusName}", portalFile, StringComparison.Ordinal);

        string serviceFile = File.ReadAllText(PortalInstall.ServiceFilePath(home.DataHome));
        Assert.Contains("Exec=/opt/rove/rove-portal", serviceFile, StringComparison.Ordinal);
    }

    [Fact]
    public void WithdrawingRemovesBothFiles()
    {
        using PortalHome home = new();
        PortalInstall.Advertise(home.DataHome, "/opt/rove/rove-portal");

        PortalInstall.Withdraw(home.DataHome, "/opt/rove/rove-portal");

        Assert.False(File.Exists(PortalInstall.PortalFilePath(home.DataHome)));
        Assert.False(File.Exists(PortalInstall.ServiceFilePath(home.DataHome)));
    }

    [Fact]
    public void WithdrawingKillsARunningPortalProcess()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using PortalHome home = new();
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("sleep", "30") { UseShellExecute = false })!;
        string exePath = new FileInfo($"/proc/{process.Id}/exe").LinkTarget!;

        PortalInstall.Withdraw(home.DataHome, exePath);

        process.WaitForExit(2000);
        Assert.True(process.HasExited);
    }
}

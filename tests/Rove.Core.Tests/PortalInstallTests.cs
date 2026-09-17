using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public sealed class PortalHome : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rove-portal-" + Guid.NewGuid().ToString("N"));

    public string DataHome => Path.Combine(Root, "share");

    public string ConfigHome => Path.Combine(Root, "config");

    public string StatePath => Path.Combine(Root, "state", "portal-install.json");

    public string AskedPath => Path.Combine(Root, "state", "portal-asked.json");

    public string ConfigPath => PortalInstall.ConfigPath(ConfigHome);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
        }
    }
}

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

public class PortalFilesTests
{
    [Fact]
    public void ThePreferredNameIsThePortalFileWithoutItsExtension()
    {
        Assert.Equal("org.freedesktop.impl.portal.Rove", PortalFiles.PreferredName);
    }
}

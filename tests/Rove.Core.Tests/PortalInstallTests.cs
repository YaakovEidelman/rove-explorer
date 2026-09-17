using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public sealed class PortalHome : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rove-portal-" + Guid.NewGuid().ToString("N"));

    public string DataHome => Path.Combine(Root, "share");

    public string ConfigHome => Path.Combine(Root, "config");

    public string StatePath => Path.Combine(Root, "state", "portal-install.json");

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
        PortalInstall.EnsureBackend(home.ConfigPath, home.StatePath, "rove");

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

public class PortalInstallEnsureBackendTests
{
    [Fact]
    public void WithNoConfigAtAllRoveClaimsFileChooser()
    {
        using PortalHome home = new();

        bool claimed = PortalInstall.EnsureBackend(home.ConfigPath, home.StatePath, "rove");

        Assert.True(claimed);
        string written = File.ReadAllText(home.ConfigPath);
        Assert.Contains("org.freedesktop.impl.portal.FileChooser=rove", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExistingConfigIsNeverTouched()
    {
        using PortalHome home = new();
        Directory.CreateDirectory(Path.GetDirectoryName(home.ConfigPath)!);
        File.WriteAllText(home.ConfigPath, "[preferred]\ndefault=gtk\n");

        bool claimed = PortalInstall.EnsureBackend(home.ConfigPath, home.StatePath, "rove");

        Assert.False(claimed);
        Assert.Equal("[preferred]\ndefault=gtk\n", File.ReadAllText(home.ConfigPath));
        Assert.False(File.Exists(home.StatePath));
    }

    [Fact]
    public void ASecondLaunchDoesNotClaimAgain()
    {
        using PortalHome home = new();
        PortalInstall.EnsureBackend(home.ConfigPath, home.StatePath, "rove");
        string firstWrite = File.ReadAllText(home.ConfigPath);

        bool claimedAgain = PortalInstall.EnsureBackend(home.ConfigPath, home.StatePath, "rove");

        Assert.False(claimedAgain);
        Assert.Equal(firstWrite, File.ReadAllText(home.ConfigPath));
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

        PortalInstall.Disable(home.DataHome, home.ConfigPath, home.StatePath);
        Assert.Equal("[preferred]\ndefault=gtk\n", File.ReadAllText(home.ConfigPath));
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

        bool disabled = PortalInstall.Disable(home.DataHome, home.ConfigPath, home.StatePath);

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

        PortalInstall.Withdraw(home.DataHome);

        Assert.False(File.Exists(PortalInstall.PortalFilePath(home.DataHome)));
        Assert.False(File.Exists(PortalInstall.ServiceFilePath(home.DataHome)));
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

using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public sealed class SettingsPortalConfirmTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "rove-settings-portal-confirm-" + Guid.NewGuid().ToString("N"));

    private SettingsViewModel New(out FilePickerPortal portal, out ConfirmViewModel confirm)
    {
        portal = new FilePickerPortal(
            Path.Combine(_root, "share"),
            Path.Combine(_root, "config", "xdg-desktop-portal", "portals.conf"),
            Path.Combine(_root, "state", "portal-install.json"),
            Path.Combine(_root, "state", "portal-asked.json"),
            Path.Combine(_root, "config", "mimeapps.list"),
            Path.Combine(_root, "state", "mime-default.json"),
            "/opt/rove/rove-portal",
            "rove.desktop");
        SettingsStore store = new(Path.Combine(_root, "settings.json"));
        CommandRegistry registry = new(KeymapLoad.Empty);
        confirm = new ConfirmViewModel(registry);
        return new SettingsViewModel(registry, store, portal, confirm);
    }

    [Fact]
    public void ActivatingItAsksBeforeClaimingAnything()
    {
        if (!OperatingSystem.IsLinux())
            return;
        SettingsViewModel settings = New(out FilePickerPortal portal, out ConfirmViewModel confirm);
        settings.Toggle();
        settings.PreviousSection();

        settings.Activate();

        Assert.True(confirm.IsOpen);
        Assert.Equal(PortalStatus.NotInstalled, portal.Status);
    }

    [Fact]
    public void AcceptingTheWarningClaimsIt()
    {
        if (!OperatingSystem.IsLinux())
            return;
        SettingsViewModel settings = New(out FilePickerPortal portal, out ConfirmViewModel confirm);
        settings.Toggle();
        settings.PreviousSection();
        settings.Activate();

        confirm.Accept();

        Assert.Equal(PortalStatus.OwnedByRove, portal.Status);
        Assert.Equal("Rove", settings.Rows[^1].Value);
    }

    [Fact]
    public void CancellingTheWarningLeavesItUnclaimed()
    {
        if (!OperatingSystem.IsLinux())
            return;
        SettingsViewModel settings = New(out FilePickerPortal portal, out ConfirmViewModel confirm);
        settings.Toggle();
        settings.PreviousSection();
        settings.Activate();

        confirm.Cancel();

        Assert.Equal(PortalStatus.NotInstalled, portal.Status);
    }

    [Fact]
    public void RevertingAlreadyClaimedDoesNotAskFirst()
    {
        if (!OperatingSystem.IsLinux())
            return;
        SettingsViewModel settings = New(out FilePickerPortal portal, out ConfirmViewModel confirm);
        settings.Toggle();
        settings.PreviousSection();
        settings.Activate();
        confirm.Accept();

        settings.Activate();

        Assert.False(confirm.IsOpen);
        Assert.Equal(PortalStatus.NotInstalled, portal.Status);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public sealed class SettingsPortalRowTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "rove-settings-portal-" + Guid.NewGuid().ToString("N"));

    private SettingsViewModel New(out FilePickerPortal portal)
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
        return new SettingsViewModel(new CommandRegistry(KeymapLoad.Empty), store, portal);
    }

    [Fact]
    public void TheRowStartsNotInstalled()
    {
        if (!OperatingSystem.IsLinux())
            return;
        SettingsViewModel settings = New(out _);
        settings.Toggle();
        settings.PreviousSection();

        Assert.Equal("Not installed", settings.Rows[^1].Value);
    }

    [Fact]
    public void ActivatingItClaimsTheFileChooserPreference()
    {
        if (!OperatingSystem.IsLinux())
            return;
        SettingsViewModel settings = New(out FilePickerPortal portal);
        settings.Toggle();
        settings.PreviousSection();

        settings.Activate();

        Assert.Equal(PortalStatus.OwnedByRove, portal.Status);
        Assert.Equal("Rove", settings.Rows[^1].Value);
    }

    [Fact]
    public void ActivatingItAgainRevertsIt()
    {
        if (!OperatingSystem.IsLinux())
            return;
        SettingsViewModel settings = New(out FilePickerPortal portal);
        settings.Toggle();
        settings.PreviousSection();
        settings.Activate();

        settings.Activate();

        Assert.Equal(PortalStatus.NotInstalled, portal.Status);
        Assert.Equal("Not installed", settings.Rows[^1].Value);
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

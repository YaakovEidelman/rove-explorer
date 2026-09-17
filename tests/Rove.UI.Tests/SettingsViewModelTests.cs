using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(), "rove-settings-" + Guid.NewGuid().ToString("N") + ".json");

    private SettingsViewModel New(out SettingsStore store)
    {
        store = new SettingsStore(_file);
        return new SettingsViewModel(new CommandRegistry(KeymapLoad.Empty), store);
    }

    private static int ExpectedRowCount => OperatingSystem.IsLinux() ? 6 : 5;

    [Fact]
    public void OpeningBuildsARowForEverySetting()
    {
        SettingsViewModel settings = New(out _);

        settings.Toggle();

        Assert.True(settings.IsOpen);
        Assert.Equal(ExpectedRowCount, settings.Rows.Count);
        Assert.Equal(0, settings.SelectedIndex);
    }

    [Fact]
    public void MovingWrapsAroundBothEnds()
    {
        SettingsViewModel settings = New(out _);
        settings.Toggle();

        settings.MoveUp();
        Assert.Equal(ExpectedRowCount - 1, settings.SelectedIndex);

        settings.MoveDown();
        Assert.Equal(0, settings.SelectedIndex);
    }

    [Fact]
    public void ActivatingTheThemeRowCyclesThroughEveryOptionAndBack()
    {
        SettingsViewModel settings = New(out SettingsStore store);
        settings.Toggle();

        string[] expected = OperatingSystem.IsLinux() && OmarchyTheme.IsAvailable
            ? ["Custom", "Omarchy", "Light", "Dark", "System"]
            : ["Custom", "Light", "Dark", "System"];

        foreach (string theme in expected)
        {
            settings.Activate();
            Assert.Equal(theme, store.Current.Theme);
        }
    }

    [Fact]
    public void ActivatingTheHiddenFilesRowFlipsItAndPersists()
    {
        SettingsViewModel settings = New(out SettingsStore store);
        settings.Toggle();
        settings.MoveDown();

        settings.Activate();

        Assert.True(store.Current.ShowHiddenByDefault);
        Assert.Equal("On", settings.Rows[1].Value);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
        }
    }
}

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

        Assert.Equal("Not installed", settings.Rows[^1].Value);
    }

    [Fact]
    public void ActivatingItClaimsTheFileChooserPreference()
    {
        if (!OperatingSystem.IsLinux())
            return;
        SettingsViewModel settings = New(out FilePickerPortal portal);
        settings.Toggle();
        settings.MoveUp();

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
        settings.MoveUp();
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

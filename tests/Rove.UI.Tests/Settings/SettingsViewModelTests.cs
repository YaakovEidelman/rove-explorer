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

    private static int ExpectedRowCount => OperatingSystem.IsLinux() ? 7 : 6;

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

using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(), "rove-settings-" + Guid.NewGuid().ToString("N") + ".json");

    private (SettingsViewModel Settings, CommandRegistry Registry, SettingsStore Store) New()
    {
        SettingsStore store = new(_file);
        CommandRegistry registry = new(KeymapLoad.Empty);
        return (new SettingsViewModel(registry, store), registry, store);
    }

    [Fact]
    public void OpeningStartsOnTheAppearanceSectionWithItsRows()
    {
        (SettingsViewModel settings, _, _) = New();

        settings.Toggle();

        Assert.True(settings.IsOpen);
        Assert.Equal(0, settings.SelectedSectionIndex);
        Assert.Equal(["Theme", "Edit custom colors…"], settings.Rows.Select(r => r.Label));
        Assert.Equal(0, settings.SelectedIndex);
    }

    [Fact]
    public void MovingWrapsAroundBothEndsOfTheCurrentSection()
    {
        (SettingsViewModel settings, _, _) = New();
        settings.Toggle();
        int count = settings.Rows.Count;

        settings.MoveUp();
        Assert.Equal(count - 1, settings.SelectedIndex);

        settings.MoveDown();
        Assert.Equal(0, settings.SelectedIndex);
    }

    [Fact]
    public void ChangingSectionsCyclesThroughAllOfThemAndWraps()
    {
        (SettingsViewModel settings, _, _) = New();
        settings.Toggle();
        int sectionCount = settings.Sections.Count;

        settings.PreviousSection();
        Assert.Equal(sectionCount - 1, settings.SelectedSectionIndex);

        settings.NextSection();
        Assert.Equal(0, settings.SelectedSectionIndex);
    }

    [Fact]
    public void ActivatingTheThemeRowCyclesThroughEveryOptionAndBack()
    {
        (SettingsViewModel settings, _, SettingsStore store) = New();
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
        (SettingsViewModel settings, _, SettingsStore store) = New();
        settings.Toggle();
        settings.NextSection();

        settings.Activate();

        Assert.True(store.Current.ShowHiddenByDefault);
        Assert.Equal("On", settings.Rows[0].Value);
    }

    [Fact]
    public void EditCustomColorsOpensTheEditorAndSwitchesToCustomTheme()
    {
        (SettingsViewModel settings, _, SettingsStore store) = New();
        settings.Toggle();
        settings.MoveDown();

        settings.Activate();

        Assert.True(settings.InThemeEditor);
        Assert.Equal("Custom", store.Current.Theme);
        Assert.NotEmpty(settings.ThemeRows);
    }

    [Fact]
    public void TypingAValidHexAppliesAndPersistsIt()
    {
        (SettingsViewModel settings, CommandRegistry registry, _) = New();
        settings.Toggle();
        settings.MoveDown();
        settings.Activate();
        settings.ThemeMoveDown();
        settings.ThemeActivate();
        Assert.True(settings.InThemeEditorField);

        settings.ThemeRows[settings.ThemeSelectedIndex].EditText = "112233";
        registry.TryExecute(CommandDef.ThemeEditorApplyField.Id);

        Assert.False(settings.InThemeEditorField);
        Assert.Equal("#112233", settings.ThemeRows[settings.ThemeSelectedIndex].Value);
    }

    [Fact]
    public void ActivatingAColorRowOpensItAsTheSelectedThemeRow()
    {
        (SettingsViewModel settings, _, _) = New();
        settings.Toggle();
        settings.MoveDown();
        settings.Activate();
        settings.ThemeMoveDown();

        settings.ThemeActivate();
        ThemeEditorRow row = settings.ThemeRows[settings.ThemeSelectedIndex];
        Assert.Same(row, settings.SelectedThemeRow);
        Assert.True(row.IsEditing);
    }

    [Fact]
    public void EditingSeedsRgbSlidersAndKeepsThemInSyncWithTheTypedHex()
    {
        (SettingsViewModel settings, _, _) = New();
        settings.Toggle();
        settings.MoveDown();
        settings.Activate();
        settings.ThemeMoveDown();

        settings.ThemeActivate();
        ThemeEditorRow row = settings.ThemeRows[settings.ThemeSelectedIndex];
        Assert.Equal(row.EditText, $"{row.Red:X2}{row.Green:X2}{row.Blue:X2}", ignoreCase: true);

        row.EditText = "00FF80";
        Assert.Equal(0, row.Red);
        Assert.Equal(255, row.Green);
        Assert.Equal(0x80, row.Blue);

        row.Red = 255;
        Assert.Equal("FFFF80", row.EditText);
    }

    [Fact]
    public void TypingAnInvalidHexLeavesEditingOpenAndSaysSo()
    {
        (SettingsViewModel settings, CommandRegistry registry, _) = New();
        settings.Toggle();
        settings.MoveDown();
        settings.Activate();
        settings.ThemeMoveDown();
        settings.ThemeActivate();

        string? info = null;
        settings.InfoRaised += m => info = m;
        settings.ThemeRows[settings.ThemeSelectedIndex].EditText = "not-a-color";
        registry.TryExecute(CommandDef.ThemeEditorApplyField.Id);

        Assert.True(settings.InThemeEditorField);
        Assert.NotNull(info);
    }

    [Fact]
    public void EscapingTheEditorReturnsToSettingsWithoutClosingTheCard()
    {
        (SettingsViewModel settings, CommandRegistry registry, _) = New();
        settings.Toggle();
        settings.MoveDown();
        settings.Activate();

        registry.TryExecute(CommandDef.ThemeEditorClose.Id);

        Assert.False(settings.InThemeEditor);
        Assert.True(settings.IsOpen);
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

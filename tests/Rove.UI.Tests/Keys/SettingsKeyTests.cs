using Avalonia.Input;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class SettingsKeyTests : HeadlessTest
{
    [Fact]
    public Task CommaOpensAndClosesSettings() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(_ => { });

        harness.Press(Key.OemComma);
        Assert.True(harness.Model.Settings.IsOpen);
        Assert.Equal(Mode.Settings, harness.Model.GetCurrentMode());

        harness.Press(Key.Escape);
        Assert.False(harness.Model.Settings.IsOpen);
        Assert.Equal(Mode.Browse, harness.Model.GetCurrentMode());
    });

    [Fact]
    public Task HAndLWalkTheSectionsAndWrap() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(_ => { });
        harness.Press(Key.OemComma);
        int sectionCount = harness.Model.Settings.Sections.Count;

        harness.Press(Key.H);
        Assert.Equal(sectionCount - 1, harness.Model.Settings.SelectedSectionIndex);

        harness.Press(Key.L);
        Assert.Equal(0, harness.Model.Settings.SelectedSectionIndex);
    });

    [Fact]
    public Task EditCustomColorsOpensTheThemeEditorOnTopOfSettings() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(_ => { });
        harness.Press(Key.OemComma);

        harness.Press(Key.J);
        harness.Press(Key.Enter);

        Assert.True(harness.Model.Settings.InThemeEditor);
        Assert.Equal(Mode.ThemeEditor, harness.Model.GetCurrentMode());
        Assert.NotEmpty(harness.Model.Settings.ThemeRows);
    });

    [Fact]
    public Task TypingAHexColorAndPressingEnterAppliesIt() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(_ => { });
        harness.Press(Key.OemComma);
        harness.Press(Key.J);
        harness.Press(Key.Enter);

        harness.Press(Key.J);
        harness.Press(Key.Enter);
        Assert.Equal(Mode.ThemeEditorField, harness.Model.GetCurrentMode());

        SettingsViewModel settings = harness.Model.Settings;
        settings.ThemeRows[settings.ThemeSelectedIndex].EditText = "#445566";
        harness.Press(Key.Enter);

        Assert.Equal(Mode.ThemeEditor, harness.Model.GetCurrentMode());
        Assert.Equal("#445566", settings.ThemeRows[settings.ThemeSelectedIndex].Value);
    });

    [Fact]
    public Task EscapeStepsOutOfTheThemeEditorBeforeClosingSettings() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(_ => { });
        harness.Press(Key.OemComma);
        harness.Press(Key.J);
        harness.Press(Key.Enter);
        Assert.Equal(Mode.ThemeEditor, harness.Model.GetCurrentMode());

        harness.Press(Key.Escape);
        Assert.False(harness.Model.Settings.InThemeEditor);
        Assert.True(harness.Model.Settings.IsOpen);
        Assert.Equal(Mode.Settings, harness.Model.GetCurrentMode());

        harness.Press(Key.Escape);
        Assert.False(harness.Model.Settings.IsOpen);
    });
}

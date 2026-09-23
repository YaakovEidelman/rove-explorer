using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;
using System.Collections.ObjectModel;

namespace Rove.UI.ViewModels;

public partial class SettingsViewModel
{
    private ThemeColors _themeColors = CustomTheme.Default;
    private readonly Dictionary<ThemeColorField, ThemeEditorRow> _themeRowsByField = [];

    [ObservableProperty]
    private bool _inThemeEditor;

    [ObservableProperty]
    private bool _inThemeEditorField;

    [ObservableProperty]
    private ObservableCollection<ThemeEditorRow> _themeRows = [];

    [ObservableProperty]
    private int _themeSelectedIndex;

    private void OpenThemeEditor()
    {
        _themeColors = CustomTheme.Load() ?? CustomTheme.Default;

        AppSettings s = _store.Current;
        if (s.Theme != "Custom")
        {
            _store.Update(s with { Theme = "Custom" });
            ApplyTheme();
            RebuildMaster();
        }

        RebuildThemeRows();
        if (ThemeSelectedIndex < 0 || ThemeSelectedIndex >= ThemeRows.Count)
            ThemeSelectedIndex = ThemeRows.Count > 0 ? 0 : -1;
        InThemeEditor = true;
    }

    private void CloseThemeEditor()
    {
        InThemeEditor = false;
        InThemeEditorField = false;
    }

    private void RebuildThemeRows()
    {
        SetThemeRow(ThemeColorField.Mode, "Base",
            string.Equals(_themeColors.Mode, "light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark",
            isColor: false);
        SetThemeRow(ThemeColorField.AppBackground, "Background", _themeColors.AppBackground, isColor: true);
        SetThemeRow(ThemeColorField.Surface, "Surface", _themeColors.Surface, isColor: true);
        SetThemeRow(ThemeColorField.SurfaceAlt, "Surface (alt)", _themeColors.SurfaceAlt, isColor: true);
        SetThemeRow(ThemeColorField.AppBorder, "Border", _themeColors.AppBorder, isColor: true);
        SetThemeRow(ThemeColorField.TextPrimary, "Text", _themeColors.TextPrimary, isColor: true);
        SetThemeRow(ThemeColorField.TextSecondary, "Text (secondary)", _themeColors.TextSecondary, isColor: true);
        SetThemeRow(ThemeColorField.Accent, "Accent", _themeColors.Accent, isColor: true);
        SetThemeRow(ThemeColorField.AccentSubtle, "Accent (subtle)", _themeColors.AccentSubtle, isColor: true);
        SetThemeRow(ThemeColorField.Error, "Error", _themeColors.Error, isColor: true);
        SetThemeRow(ThemeColorField.MarkBar, "Mark bar", _themeColors.MarkBar, isColor: true);
    }

    private void SetThemeRow(ThemeColorField field, string label, string value, bool isColor)
    {
        if (_themeRowsByField.TryGetValue(field, out ThemeEditorRow? row))
        {
            row.Value = value;
            row.Swatch = isColor ? TryBrush(value) : null;
            row.IsEditing = false;
            return;
        }

        row = new ThemeEditorRow(field, label, value, isColor, isColor ? TryBrush(value) : null);
        _themeRowsByField[field] = row;
        ThemeRows.Add(row);
    }

    private static IBrush? TryBrush(string hex)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public void ThemeMoveUp()
    {
        if (ThemeRows.Count == 0)
            return;
        ThemeSelectedIndex = ThemeSelectedIndex <= 0 ? ThemeRows.Count - 1 : ThemeSelectedIndex - 1;
    }

    public void ThemeMoveDown()
    {
        if (ThemeRows.Count == 0)
            return;
        ThemeSelectedIndex = ThemeSelectedIndex >= ThemeRows.Count - 1 ? 0 : ThemeSelectedIndex + 1;
    }

    public void ThemeActivate()
    {
        if (ThemeSelectedIndex < 0 || ThemeSelectedIndex >= ThemeRows.Count)
            return;

        ThemeEditorRow row = ThemeRows[ThemeSelectedIndex];
        if (row.Field == ThemeColorField.Mode)
        {
            _themeColors = _themeColors with
            {
                Mode = string.Equals(_themeColors.Mode, "light", StringComparison.OrdinalIgnoreCase) ? "dark" : "light",
            };
            SaveThemeColors();
            RebuildThemeRows();
            return;
        }

        row.EditText = row.Value;
        row.IsEditing = true;
        InThemeEditorField = true;
    }

    public void ApplyThemeField()
    {
        if (ThemeSelectedIndex < 0 || ThemeSelectedIndex >= ThemeRows.Count)
        {
            InThemeEditorField = false;
            return;
        }

        ThemeEditorRow row = ThemeRows[ThemeSelectedIndex];
        string typed = row.EditText.Trim();
        try
        {
            _ = Color.Parse(typed);
        }
        catch (FormatException)
        {
            InfoRaised?.Invoke($"\"{typed}\" isn't a color — try a hex value like #2F6FDE.");
            return;
        }

        _themeColors = row.Field switch
        {
            ThemeColorField.AppBackground => _themeColors with { AppBackground = typed },
            ThemeColorField.Surface => _themeColors with { Surface = typed },
            ThemeColorField.SurfaceAlt => _themeColors with { SurfaceAlt = typed },
            ThemeColorField.AppBorder => _themeColors with { AppBorder = typed },
            ThemeColorField.TextPrimary => _themeColors with { TextPrimary = typed },
            ThemeColorField.TextSecondary => _themeColors with { TextSecondary = typed },
            ThemeColorField.Accent => _themeColors with { Accent = typed },
            ThemeColorField.AccentSubtle => _themeColors with { AccentSubtle = typed },
            ThemeColorField.Error => _themeColors with { Error = typed },
            ThemeColorField.MarkBar => _themeColors with { MarkBar = typed },
            _ => _themeColors,
        };

        row.IsEditing = false;
        InThemeEditorField = false;
        SaveThemeColors();
        RebuildThemeRows();
    }

    public void CancelThemeField()
    {
        if (ThemeSelectedIndex >= 0 && ThemeSelectedIndex < ThemeRows.Count)
            ThemeRows[ThemeSelectedIndex].IsEditing = false;
        InThemeEditorField = false;
    }

    private void SaveThemeColors()
    {
        CustomTheme.Save(_themeColors);
        ApplyTheme();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;
using System.Collections.ObjectModel;

namespace Rove.UI.ViewModels;

public partial class SettingsViewModel
{
    public static readonly string[] DefaultViewOptions = ["List", "Small icons", "Medium icons", "Large icons"];

    private SettingsRowKind _choiceKind;

    [ObservableProperty]
    private bool _inChoice;

    [ObservableProperty]
    private string _choiceTitle = "";

    [ObservableProperty]
    private ObservableCollection<string> _choiceOptions = [];

    [ObservableProperty]
    private int _choiceSelectedIndex;

    private static bool IsChoice(SettingsRowKind kind) =>
        kind is SettingsRowKind.Theme or SettingsRowKind.DefaultView;

    private void OpenChoice(SettingsRow row)
    {
        string[] options = row.Kind == SettingsRowKind.Theme ? ThemeOptions() : DefaultViewOptions;
        _choiceKind = row.Kind;
        ChoiceTitle = row.Label;
        ChoiceOptions = new ObservableCollection<string>(options);
        ChoiceSelectedIndex = Math.Max(0, Array.IndexOf(options, row.Value));
        InChoice = true;
    }

    public void ChoiceMoveUp()
    {
        if (ChoiceOptions.Count == 0)
            return;
        ChoiceSelectedIndex = ChoiceSelectedIndex <= 0 ? ChoiceOptions.Count - 1 : ChoiceSelectedIndex - 1;
        PreviewChoice();
    }

    public void ChoiceMoveDown()
    {
        if (ChoiceOptions.Count == 0)
            return;
        ChoiceSelectedIndex = ChoiceSelectedIndex >= ChoiceOptions.Count - 1 ? 0 : ChoiceSelectedIndex + 1;
        PreviewChoice();
    }

    private void PreviewChoice()
    {
        if (_choiceKind == SettingsRowKind.Theme)
            ThemePalette.ApplyFromSettings(_store.Current with { Theme = ChoiceOptions[ChoiceSelectedIndex] });
    }

    public void PickChoice()
    {
        if (!InChoice || ChoiceSelectedIndex < 0 || ChoiceSelectedIndex >= ChoiceOptions.Count)
            return;

        string picked = ChoiceOptions[ChoiceSelectedIndex];
        AppSettings s = _store.Current;
        _store.Update(_choiceKind == SettingsRowKind.Theme
            ? s with { Theme = picked }
            : s with { DefaultView = picked });
        InChoice = false;
        ApplyTheme();
        RebuildMaster();
    }

    public void CancelChoice()
    {
        if (!InChoice)
            return;
        InChoice = false;
        ApplyTheme();
    }

    private static string[] ThemeOptions() =>
        OperatingSystem.IsLinux() && OmarchyTheme.IsAvailable
            ? ["Light", "Dark", "System", "Custom", "Omarchy"]
            : ["Light", "Dark", "System", "Custom"];
}

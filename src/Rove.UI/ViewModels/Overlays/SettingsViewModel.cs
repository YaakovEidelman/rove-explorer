using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;

namespace Rove.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly CommandRegistry _registry;
    private readonly SettingsStore _store;
    private readonly FilePickerPortal? _portal;
    private readonly ConfirmViewModel? _confirm;

    private readonly List<SettingsRow> _master = [];
    private readonly Dictionary<SettingsRowKind, SettingsRow> _rowsByKind = [];

    public event Action<string>? InfoRaised;

    public SettingsViewModel(
        CommandRegistry registry, SettingsStore store, FilePickerPortal? portal = null, ConfirmViewModel? confirm = null)
    {
        _registry = registry;
        _store = store;
        _portal = portal ?? (OperatingSystem.IsLinux() ? new FilePickerPortal() : null);
        _confirm = confirm;
        RegisterBindings();
        ApplyTheme();
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private ObservableCollection<SettingsRow> _rows = [];

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private ObservableCollection<string> _sections = [];

    [ObservableProperty]
    private int _selectedSectionIndex;

    [ObservableProperty]
    private ObservableCollection<SettingsSectionTab> _sectionTabs = [];

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        RebuildMaster();
        SelectedSectionIndex = 0;
        RebuildSections();
        RebuildVisibleRows();
        IsOpen = true;
    }

    private void Close()
    {
        IsOpen = false;
        CloseThemeEditor();
    }

    private void RebuildMaster()
    {
        AppSettings s = _store.Current;
        SetRow(SettingsRowKind.Theme, "Theme", s.Theme, "Appearance");
        SetRow(SettingsRowKind.EditCustomColors, "Edit custom colors…", "", "Appearance");
        SetRow(SettingsRowKind.ShowHidden, "Show hidden files by default", s.ShowHiddenByDefault ? "On" : "Off",
            "Behavior", isToggle: true, isOn: s.ShowHiddenByDefault);
        SetRow(SettingsRowKind.DefaultView, "Default view", s.DefaultView, "Behavior");
        SetRow(SettingsRowKind.SortDownloadsByTime, "Sort the Downloads folder by time",
            s.SortDownloadsByTime ? "On" : "Off", "Behavior", isToggle: true, isOn: s.SortDownloadsByTime);
        SetRow(SettingsRowKind.GroupByDate, "Group by date when sorted by date modified",
            s.GroupByDate ? "On" : "Off", "Behavior", isToggle: true, isOn: s.GroupByDate);
        SetRow(SettingsRowKind.AutoUpdate, "Auto-update", s.AutoUpdate ? "On" : "Off",
            "Updates", isToggle: true, isOn: s.AutoUpdate);
        if (OperatingSystem.IsLinux() && _portal is not null)
            SetRow(SettingsRowKind.PortalIntegration, "Default for opening files and folders",
                PortalStatusLabel(_portal.Status), "Integration");
    }

    private void SetRow(SettingsRowKind kind, string label, string value, string section,
        bool isToggle = false, bool isOn = false)
    {
        if (_rowsByKind.TryGetValue(kind, out SettingsRow? row))
        {
            row.Label = label;
            row.Value = value;
            row.IsToggle = isToggle;
            row.IsOn = isOn;
            return;
        }

        row = new SettingsRow(kind, label, value, section, isToggle, isOn);
        _rowsByKind[kind] = row;
        _master.Add(row);
    }

    private void RebuildSections()
    {
        List<string> sections = [];
        foreach (SettingsRow row in _master)
        {
            if (!sections.Contains(row.Section))
                sections.Add(row.Section);
        }
        Sections = new ObservableCollection<string>(sections);
        SectionTabs = new ObservableCollection<SettingsSectionTab>(
            sections.Select((name, index) => new SettingsSectionTab(name, index == SelectedSectionIndex)));
    }

    private void RebuildVisibleRows()
    {
        string? current = SelectedSectionIndex >= 0 && SelectedSectionIndex < Sections.Count
            ? Sections[SelectedSectionIndex]
            : null;
        Rows = new ObservableCollection<SettingsRow>(_master.Where(r => r.Section == current));
        SelectedIndex = Rows.Count > 0 ? 0 : -1;
    }

    private static string PortalStatusLabel(PortalStatus status) => status switch
    {
        PortalStatus.OwnedByRove => "Rove",
        PortalStatus.OwnedByOther => "Owned by something else",
        _ => "Not installed",
    };

    public void MoveUp()
    {
        if (Rows.Count == 0)
            return;
        SelectedIndex = SelectedIndex <= 0 ? Rows.Count - 1 : SelectedIndex - 1;
    }

    public void MoveDown()
    {
        if (Rows.Count == 0)
            return;
        SelectedIndex = SelectedIndex >= Rows.Count - 1 ? 0 : SelectedIndex + 1;
    }

    public void NextSection() => ChangeSection(1);

    public void PreviousSection() => ChangeSection(-1);

    private void ChangeSection(int direction)
    {
        if (Sections.Count == 0)
            return;
        SelectedSectionIndex = (SelectedSectionIndex + direction + Sections.Count) % Sections.Count;
        RebuildSections();
        RebuildVisibleRows();
    }

    public void Activate()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Rows.Count)
            return;

        SettingsRow row = Rows[SelectedIndex];

        if (row.Kind == SettingsRowKind.EditCustomColors)
        {
            OpenThemeEditor();
            return;
        }

        if (OperatingSystem.IsLinux() && row.Kind == SettingsRowKind.PortalIntegration && _portal is not null)
        {
            if (_portal.Status == PortalStatus.OwnedByRove)
            {
                _portal.Disable();
                RebuildMaster();
            }
            else if (_confirm is not null)
                ConfirmAndEnablePortal(_portal, _confirm);
            else
            {
                _portal.Enable();
                RebuildMaster();
            }
            return;
        }

        AppSettings s = _store.Current;
        _store.Update(row.Kind switch
        {
            SettingsRowKind.Theme => s with { Theme = NextTheme(s.Theme) },
            SettingsRowKind.ShowHidden => s with { ShowHiddenByDefault = !s.ShowHiddenByDefault },
            SettingsRowKind.DefaultView => s with { DefaultView = s.DefaultView == "Icons" ? "List" : "Icons" },
            SettingsRowKind.SortDownloadsByTime => s with { SortDownloadsByTime = !s.SortDownloadsByTime },
            SettingsRowKind.GroupByDate => s with { GroupByDate = !s.GroupByDate },
            SettingsRowKind.AutoUpdate => s with { AutoUpdate = !s.AutoUpdate },
            _ => s,
        });

        if (row.Kind == SettingsRowKind.Theme)
            ApplyTheme();

        RebuildMaster();
    }

    [SupportedOSPlatform("linux")]
    private void ConfirmAndEnablePortal(FilePickerPortal portal, ConfirmViewModel confirm) =>
        confirm.Request(FilePickerPortal.ClaimWarning, () =>
        {
            portal.Enable();
            RebuildMaster();
        });

    private static string NextTheme(string theme)
    {
        string[] options = OperatingSystem.IsLinux() && OmarchyTheme.IsAvailable
            ? ["Light", "Dark", "System", "Custom", "Omarchy"]
            : ["Light", "Dark", "System", "Custom"];
        int index = Array.IndexOf(options, theme);
        return options[(index + 1) % options.Length];
    }

    public void ApplyTheme() => ThemePalette.ApplyFromSettings(_store.Current);

    private void RegisterBindings()
    {
        _registry.Register(CommandDef.ShowSettings, Toggle);
        _registry.Register(CommandDef.SettingsMoveUp, MoveUp);
        _registry.Register(CommandDef.SettingsMoveDown, MoveDown);
        _registry.Register(CommandDef.SettingsActivate, Activate);
        _registry.Register(CommandDef.SettingsNextSection, NextSection);
        _registry.Register(CommandDef.SettingsPreviousSection, PreviousSection);
        _registry.Register(CommandDef.ThemeEditorMoveUp, ThemeMoveUp);
        _registry.Register(CommandDef.ThemeEditorMoveDown, ThemeMoveDown);
        _registry.Register(CommandDef.ThemeEditorActivate, ThemeActivate);
        _registry.Register(CommandDef.ThemeEditorClose, CloseThemeEditor);
        _registry.Register(CommandDef.ThemeEditorApplyField, ApplyThemeField);
        _registry.Register(CommandDef.ThemeEditorCancelField, CancelThemeField);
    }
}

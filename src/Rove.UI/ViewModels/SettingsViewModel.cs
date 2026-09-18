using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;

namespace Rove.UI.ViewModels;

/// <summary>
/// One row of the settings list: what it's called, its current value, and
/// how to draw it — a section header above it, and a toggle switch instead
/// of a plain value chip when it's an on/off setting. Display only: row
/// order/count here is what MoveUp/MoveDown/Activate index into, unchanged.
///
/// <para>
/// A mutable, notifying object rather than a record Rebuild() replaces
/// wholesale — the toggle switch's on/off transition (App.axaml) animates a
/// property changing on a control that's still there, not a control created
/// already in its final state because the row underneath it got swapped out.
/// </para>
/// </summary>
public partial class SettingsRow : ObservableObject
{
    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSection))]
    private string? _section;

    [ObservableProperty]
    private bool _isToggle;

    [ObservableProperty]
    private bool _isOn;

    public bool HasSection => Section is not null;

    public SettingsRow(string label, string value, string? section = null, bool isToggle = false, bool isOn = false)
    {
        _label = label;
        _value = value;
        _section = section;
        _isToggle = isToggle;
        _isOn = isOn;
    }
}

/// <summary>
/// The settings list, which only exists while it is on screen — the same
/// shape as bookmarks and the palette: Up/Down to move, one key to act.
/// There is nothing to type here, so acting cycles the highlighted row to
/// its next value instead of opening it.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly CommandRegistry _registry;
    private readonly SettingsStore _store;
    private readonly FilePickerPortal? _portal;
    private readonly ConfirmViewModel? _confirm;

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

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        Rebuild();
        SelectedIndex = Rows.Count > 0 ? 0 : -1;
        IsOpen = true;
    }

    private void Close() => IsOpen = false;

    private void Rebuild()
    {
        AppSettings s = _store.Current;
        int count = 0;
        SetRow(count++, "Theme", s.Theme, section: "Appearance");
        SetRow(count++, "Show hidden files by default", s.ShowHiddenByDefault ? "On" : "Off",
            section: "Behavior", isToggle: true, isOn: s.ShowHiddenByDefault);
        SetRow(count++, "Default view", s.DefaultView);
        SetRow(count++, "Sort the Downloads folder by time", s.SortDownloadsByTime ? "On" : "Off",
            isToggle: true, isOn: s.SortDownloadsByTime);
        SetRow(count++, "Auto-update", s.AutoUpdate ? "On" : "Off",
            section: "Updates", isToggle: true, isOn: s.AutoUpdate);
        if (OperatingSystem.IsLinux() && _portal is not null)
            SetRow(count++, "Default for opening files and folders", PortalStatusLabel(_portal.Status),
                section: "Integration");

        while (Rows.Count > count)
            Rows.RemoveAt(Rows.Count - 1);
    }

    /// <summary>
    /// Updates the row already at <paramref name="index"/> in place rather than
    /// replacing it — same reasoning as keeping <see cref="Rows"/> itself
    /// stable (see git history): a fresh object at that slot is a fresh
    /// control underneath it, and a toggle switch's on/off transition
    /// (App.axaml) needs the same control to animate from.
    /// </summary>
    private void SetRow(int index, string label, string value, string? section = null, bool isToggle = false,
        bool isOn = false)
    {
        if (index < Rows.Count)
        {
            SettingsRow row = Rows[index];
            row.Label = label;
            row.Value = value;
            row.Section = section;
            row.IsToggle = isToggle;
            row.IsOn = isOn;
        }
        else
        {
            Rows.Add(new SettingsRow(label, value, section, isToggle, isOn));
        }
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

    /// <summary>Cycles the highlighted setting to its next value and saves it.</summary>
    public void Activate()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Rows.Count)
            return;

        if (OperatingSystem.IsLinux() && _portal is not null && SelectedIndex == Rows.Count - 1)
        {
            if (_portal.Status == PortalStatus.OwnedByRove)
            {
                _portal.Disable();
                RebuildKeepingSelection();
            }
            else if (_confirm is not null)
                ConfirmAndEnablePortal(_portal, _confirm);
            else
            {
                _portal.Enable();
                RebuildKeepingSelection();
            }
            return;
        }

        AppSettings s = _store.Current;
        _store.Update(SelectedIndex switch
        {
            0 => s with { Theme = NextTheme(s.Theme) },
            1 => s with { ShowHiddenByDefault = !s.ShowHiddenByDefault },
            2 => s with { DefaultView = s.DefaultView == "Icons" ? "List" : "Icons" },
            3 => s with { SortDownloadsByTime = !s.SortDownloadsByTime },
            4 => s with { AutoUpdate = !s.AutoUpdate },
            _ => s,
        });

        if (SelectedIndex == 0)
            ApplyTheme();

        RebuildKeepingSelection();
    }

    private void RebuildKeepingSelection()
    {
        int selected = SelectedIndex;
        Rebuild();
        SelectedIndex = selected;
    }

    [SupportedOSPlatform("linux")]
    private void ConfirmAndEnablePortal(FilePickerPortal portal, ConfirmViewModel confirm) =>
        confirm.Request(FilePickerPortal.ClaimWarning, () =>
        {
            portal.Enable();
            RebuildKeepingSelection();
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
    }
}

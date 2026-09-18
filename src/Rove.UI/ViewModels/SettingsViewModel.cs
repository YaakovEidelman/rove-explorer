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
/// </summary>
public record SettingsRow(string Label, string Value, string? Section = null, bool IsToggle = false, bool IsOn = false)
{
    public bool HasSection => Section is not null;
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
        List<SettingsRow> rows =
        [
            new("Theme", s.Theme, Section: "Appearance"),
            new("Show hidden files by default", s.ShowHiddenByDefault ? "On" : "Off",
                Section: "Behavior", IsToggle: true, IsOn: s.ShowHiddenByDefault),
            new("Default view", s.DefaultView),
            new("Sort the Downloads folder by time", s.SortDownloadsByTime ? "On" : "Off",
                IsToggle: true, IsOn: s.SortDownloadsByTime),
            new("Auto-update", s.AutoUpdate ? "On" : "Off",
                Section: "Updates", IsToggle: true, IsOn: s.AutoUpdate),
        ];
        if (OperatingSystem.IsLinux() && _portal is not null)
            rows.Add(new("Default for opening files and folders", PortalStatusLabel(_portal.Status),
                Section: "Integration"));

        // Update the existing collection in place rather than assigning a new
        // one: reassigning made the ListBox drop and rebuild every container
        // on every single toggle, which — combined with rows of differing
        // height (section headers, toggle switches) — made the card visibly
        // shrink and snap back each press instead of just refreshing values.
        for (int i = 0; i < rows.Count; i++)
        {
            if (i < Rows.Count)
                Rows[i] = rows[i];
            else
                Rows.Add(rows[i]);
        }
        while (Rows.Count > rows.Count)
            Rows.RemoveAt(Rows.Count - 1);
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

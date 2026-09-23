using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;
using System.Collections.ObjectModel;

namespace Rove.UI.ViewModels;

public partial class PaletteViewModel : ViewModelBase
{
    private const int RecentCapacity = 5;

    private readonly CommandRegistry _registry;
    private readonly SettingsStore? _settings;

    private string _scope = string.Empty;
    private readonly List<string> _recentIds;

    public event Action? Opening;

    public event Action? Executing;

    public event Action? Executed;

    public PaletteViewModel(CommandRegistry registry, SettingsStore? settings = null)
    {
        _registry = registry;
        _settings = settings;
        _recentIds = [.. settings?.Current.RecentCommandIds ?? []];
        RegisterBindings();
    }

    [ObservableProperty]
    private bool _isPaletteOpen;

    [ObservableProperty]
    private ObservableCollection<PaletteRow> _items = [];

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string _paletteSearchText = string.Empty;

    [ObservableProperty]
    private string _placeholder = DefaultPlaceholder;

    private const string DefaultPlaceholder = "type a command…";

    partial void OnPaletteSearchTextChanged(string value)
    {
        if (!IsPaletteOpen)
            return;
        Rebuild();
    }

    private PaletteEntry ToEntry(Command c) =>
        new(c, _registry.HintFor(c.Def.Id), BuildTitleSegments(PaletteSearchText, c.Def.Title), c.IsRunnable);

    private static IReadOnlyList<TitleSegment> BuildTitleSegments(string query, string title)
    {
        HashSet<int> matched = [];
        foreach (string word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (FuzzyMatcher.TryMatchWithPositions(word, title, out _, out int[] positions))
                foreach (int p in positions)
                    matched.Add(p);
        }
        if (matched.Count == 0)
            return [new TitleSegment(title, false)];

        List<TitleSegment> segments = [];
        int start = 0;
        bool inMatch = matched.Contains(0);
        for (int i = 1; i <= title.Length; i++)
        {
            bool atMatch = i < title.Length && matched.Contains(i);
            if (i == title.Length || atMatch != inMatch)
            {
                segments.Add(new TitleSegment(title[start..i], inMatch));
                start = i;
                inMatch = atMatch;
            }
        }
        return segments;
    }

    private bool InScope(Command c) =>
        _scope.Length == 0
            ? !CommandDef.IsTransient(c.Def.Id)
            : c.Def.Id.StartsWith(_scope, StringComparison.Ordinal);

    private void Rebuild()
    {
        Command[] matches = [.. _registry.FilteredCommands(PaletteSearchText).Where(InScope)];

        List<PaletteRow> rows = string.IsNullOrWhiteSpace(PaletteSearchText) && _scope.Length == 0
            ? GroupedRows(matches)
            : [.. matches.Select(c => new PaletteRow(null, ToEntry(c)))];

        Items.Clear();
        foreach (PaletteRow row in rows)
            Items.Add(row);
        ResetSelection();
    }

    private List<PaletteRow> GroupedRows(Command[] matches)
    {
        List<PaletteRow> rows = [];

        HashSet<string> recentIds = [.. _recentIds];
        Command[] recent = [.. _recentIds
            .Select(id => _registry.TryGetCommand(id, out Command c) &&
                c.Def.CommandKind == CommandKind.User && InScope(c)
                    ? (Command?)c
                    : null)
            .Where(c => c is not null)
            .Select(c => c!.Value)];

        if (recent.Length > 0)
        {
            rows.Add(new PaletteRow("Recent", null));
            foreach (Command c in recent)
                rows.Add(new PaletteRow(null, ToEntry(c)));
        }

        string? currentCategory = null;
        foreach (Command c in matches.Where(c => !recentIds.Contains(c.Def.Id)))
        {
            string category = c.Def.Category.ToString();
            if (category != currentCategory)
            {
                rows.Add(new PaletteRow(category, null));
                currentCategory = category;
            }
            rows.Add(new PaletteRow(null, ToEntry(c)));
        }

        return rows;
    }

    public void ExecuteOption()
    {
        if (Items.Count == 0 || SelectedIndex < 0 || SelectedIndex >= Items.Count)
            return;
        if (!Items[SelectedIndex].IsSelectable)
            return;
        PaletteEntry? selected = Items[SelectedIndex].Entry;
        if (selected is null)
            return;
        RememberRecent(selected.Command.Def.Id);
        Executing?.Invoke();
        try
        {
            TogglePalette();
            _registry.TryExecute(selected.Command.Def.Id);
        }
        finally
        {
            Executed?.Invoke();
        }
    }

    private void RememberRecent(string commandId)
    {
        if (CommandDef.IsTransient(commandId))
            return;
        _recentIds.Remove(commandId);
        _recentIds.Insert(0, commandId);
        if (_recentIds.Count > RecentCapacity)
            _recentIds.RemoveRange(RecentCapacity, _recentIds.Count - RecentCapacity);
        if (_settings is { } settings)
            settings.Update(settings.Current with { RecentCommandIds = [.. _recentIds] });
    }

    public void Refresh()
    {
        if (IsPaletteOpen)
            Rebuild();
    }

    public void TogglePalette()
    {
        if (IsPaletteOpen)
            Close();
        else
            Open(string.Empty, DefaultPlaceholder);
    }

    public void OpenScoped(string idPrefix, string placeholder)
    {
        Close();
        Open(idPrefix, placeholder);
    }

    private void Open(string scope, string placeholder)
    {
        _scope = scope;
        Placeholder = placeholder;
        Opening?.Invoke();
        PaletteSearchText = string.Empty;
        IsPaletteOpen = true;
        Rebuild();
    }

    private void Close()
    {
        IsPaletteOpen = false;
        _scope = string.Empty;
        Placeholder = DefaultPlaceholder;
        PaletteSearchText = string.Empty;
        Items.Clear();
        ResetSelection();
    }

    public void PaletteMoveUp()
    {
        for (int step = 0, i = SelectedIndex; step < Items.Count; step++)
        {
            i = i <= 0 ? Items.Count - 1 : i - 1;
            if (Items[i].IsSelectable)
            {
                SelectedIndex = i;
                return;
            }
        }
    }

    public void PaletteMoveDown()
    {
        for (int step = 0, i = SelectedIndex; step < Items.Count; step++)
        {
            i = i >= Items.Count - 1 ? 0 : i + 1;
            if (Items[i].IsSelectable)
            {
                SelectedIndex = i;
                return;
            }
        }
    }

    private void ResetSelection()
    {
        SelectedIndex = -1;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].IsSelectable)
            {
                SelectedIndex = i;
                break;
            }
        }
    }

    private void RegisterBindings()
    {
        _registry.Register(CommandDef.TogglePalette, TogglePalette);
        _registry.Register(CommandDef.PaletteMoveUp, PaletteMoveUp);
        _registry.Register(CommandDef.PaletteMoveDown, PaletteMoveDown);
        _registry.Register(CommandDef.PaletteExecute, ExecuteOption);
    }
}

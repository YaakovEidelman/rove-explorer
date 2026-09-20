using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Rove.UI.ViewModels;

public record PaletteEntry(Command Command, string Hint);

public partial class PaletteViewModel : ViewModelBase
{
    private readonly CommandRegistry _registry;

    private string _scope = string.Empty;

    public event Action? Opening;

    public event Action? Executing;

    public event Action? Executed;

    public PaletteViewModel(CommandRegistry registry)
    {
        _registry = registry;
        RegisterBindings();
    }

    [ObservableProperty]
    private bool _isPaletteOpen;

    [ObservableProperty]
    private ObservableCollection<PaletteEntry> _items = [];

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

    private PaletteEntry ToEntry(Command c) => new(c, _registry.HintFor(c.Def.Id));

    private bool InScope(Command c) =>
        _scope.Length == 0
            ? !CommandDef.IsTransient(c.Def.Id)
            : c.Def.Id.StartsWith(_scope, StringComparison.Ordinal);

    private void Rebuild()
    {
        Items = [.. _registry.FilteredCommands(PaletteSearchText).Where(InScope).Select(ToEntry)];
        ResetSelection();
    }

    public void ExecuteOption()
    {
        if (Items.Count == 0 || SelectedIndex < 0 || SelectedIndex >= Items.Count)
            return;
        PaletteEntry selected = Items[SelectedIndex];
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
        Items = [];
        ResetSelection();
    }

    public void PaletteMoveUp()
    {
        if (Items.Count == 0)
            return;
        SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
    }

    public void PaletteMoveDown()
    {
        if (Items.Count == 0)
            return;
        SelectedIndex = SelectedIndex >= Items.Count - 1 ? 0 : SelectedIndex + 1;
    }

    private void ResetSelection()
    {
        SelectedIndex = -1;
        if (Items.Count > 0)
            SelectedIndex = 0;
    }

    private void RegisterBindings()
    {
        _registry.Register(CommandDef.TogglePalette, TogglePalette);
        _registry.Register(CommandDef.PaletteMoveUp, PaletteMoveUp);
        _registry.Register(CommandDef.PaletteMoveDown, PaletteMoveDown);
        _registry.Register(CommandDef.PaletteExecute, ExecuteOption);
    }
}

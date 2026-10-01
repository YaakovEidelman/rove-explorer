using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;
using System.Collections.ObjectModel;

namespace Rove.UI.ViewModels;

public partial class ServersViewModel : ViewModelBase
{
    private const string NoGio = "Connecting to servers needs gio (part of GLib), which isn't available here.";

    private readonly ServerStore _store;
    private readonly SettingsStore _settings;
    private readonly ISecretStore? _secrets;
    private MountsViewModel? _mounts;
    private IMountPrompter? _prompter;
    private MountEntry[] _connected = [];

    public event Action<string>? InfoRaised;

    public event Action<string>? ErrorRaised;

    public ServersViewModel(ICommandTarget registry, ServerStore store, SettingsStore settings, ISecretStore? secrets)
    {
        _store = store;
        _settings = settings;
        _secrets = secrets;
        _store.Changed += () =>
        {
            if (IsOpen)
                Rebuild();
        };
        RegisterBindings(registry);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocusSearch))]
    private bool _isOpen;

    [ObservableProperty]
    private ObservableCollection<ServerRow> _items = [];

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    private bool _isEmpty;

    public bool FocusSearch => IsOpen && !InForm;

    public bool ShowEmpty => IsEmpty && !InForm;

    public bool CanSavePasswords => _secrets is not null;

    public void Attach(MountsViewModel mounts, IMountPrompter prompter)
    {
        _mounts = mounts;
        _prompter = prompter;
        _connected = mounts.Servers;
        mounts.ServersChanged += entries =>
        {
            _connected = entries;
            if (IsOpen)
                Rebuild();
        };
    }

    partial void OnSearchTextChanged(string value)
    {
        if (IsOpen)
            Rebuild();
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void OpenNew()
    {
        if (!IsOpen)
            Open();
        OpenForm(null);
    }

    private void Open()
    {
        SearchText = string.Empty;
        IsOpen = true;
        Rebuild();
    }

    private void Close()
    {
        CloseForm();
        IsOpen = false;
        SearchText = string.Empty;
        Items = [];
        SelectedIndex = -1;
    }

    private void Rebuild()
    {
        string? keep = Selected?.Server?.Address ?? Selected?.Connection?.MountUri;
        List<ServerRow> rows = [];
        foreach (SavedServer server in _store.Items)
        {
            MountEntry? live = _connected.FirstOrDefault(c => c.MountUri is { } uri
                && MountAddress.SameServer(uri, server.Address));
            rows.Add(new ServerRow(false, server, live, server.Name, server.Address));
        }
        foreach (MountEntry live in _connected)
        {
            if (rows.All(row => !ReferenceEquals(row.Connection, live)))
                rows.Add(new ServerRow(false, null, live, live.Name, live.MountUri ?? ""));
        }

        ServerRow[] shown = [.. rows.Where(Matches)];
        Items = [new ServerRow(true, null, null, "", ""), .. shown];
        IsEmpty = shown.Length == 0;

        int kept = keep is null ? -1 : Array.FindIndex(shown, row =>
            row.Server?.Address == keep || row.Connection?.MountUri == keep);
        SelectedIndex = kept >= 0 ? kept + 1 : shown.Length > 0 ? 1 : 0;
    }

    private bool Matches(ServerRow row) =>
        SearchText.Length == 0
        || FuzzyMatcher.TryMatch(SearchText, row.Title, out _)
        || FuzzyMatcher.TryMatch(SearchText, row.Detail, out _);

    private ServerRow? Selected =>
        SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    public void MoveUp()
    {
        if (Items.Count > 0)
            SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
    }

    public void MoveDown()
    {
        if (Items.Count > 0)
            SelectedIndex = SelectedIndex >= Items.Count - 1 ? 0 : SelectedIndex + 1;
    }

    public void ConnectSelected()
    {
        if (Selected is not { } row)
            return;
        if (row.IsAddNew)
        {
            OpenForm(null);
            return;
        }
        Close();
        if (row.Server is { } server)
            _ = ConnectSavedAsync(server);
        else if (row.Connection?.MountUri is { } uri)
            _ = ConnectAsync(uri, null);
    }

    public void EditSelected()
    {
        if (Selected?.Server is { } server)
            OpenForm(server);
        else if (Selected is { IsAddNew: false } row)
            InfoRaised?.Invoke($"{row.Title} isn't saved. Connect to it from a new server to save it.");
    }

    public void ForgetSelected()
    {
        if (Selected?.Server is not { } server)
            return;
        _store.Remove(server.Address);
        if (server.SavePassword && _secrets is not null)
            _ = _secrets.DeleteAsync(server.Address, CancellationToken.None);
        InfoRaised?.Invoke($"Forgot {server.Name}.");
    }

    public void DisconnectSelected()
    {
        if (Selected is not { IsAddNew: false } row)
            return;
        if (row.Connection is not { } live)
        {
            InfoRaised?.Invoke($"{row.Title} isn't connected.");
            return;
        }
        _ = _mounts?.DisconnectAsync(live);
    }

    private async Task ConnectSavedAsync(SavedServer server)
    {
        string? password = server.SavePassword && _secrets is not null
            ? await _secrets.LookupAsync(server.Address, CancellationToken.None)
            : null;
        SavedAnswerPrompter? prompter = await ConnectAsync(server.Address, password);
        if (prompter is { TypedPassword: { Length: > 0 } typed } && server.SavePassword)
            await SavePasswordAsync(server, typed);
    }

    private async Task<SavedAnswerPrompter?> ConnectAsync(string address, string? password)
    {
        if (_mounts is null || _prompter is null)
        {
            ErrorRaised?.Invoke(NoGio);
            return null;
        }
        SavedAnswerPrompter prompter = new(_prompter, null, password);
        return await _mounts.ConnectAsync(address, prompter) ? prompter : null;
    }

    private async Task SavePasswordAsync(SavedServer server, string password)
    {
        if (_secrets is null
            || !await _secrets.SaveAsync(server.Address, $"Rove server {server.Name}", password, CancellationToken.None))
            ErrorRaised?.Invoke($"Couldn't save the password for {server.Name} in the keyring.");
    }

    private void RegisterBindings(ICommandTarget registry)
    {
        registry.Register(CommandDef.ShowServers, Toggle);
        registry.Register(CommandDef.ConnectToServer, OpenNew);
        registry.Register(CommandDef.ServersMoveUp, MoveUp);
        registry.Register(CommandDef.ServersMoveDown, MoveDown);
        registry.Register(CommandDef.ServersExecute, ConnectSelected);
        registry.Register(CommandDef.ServersEdit, EditSelected);
        registry.Register(CommandDef.ServersForget, ForgetSelected);
        registry.Register(CommandDef.ServersDisconnect, DisconnectSelected);
        registry.Register(CommandDef.ServerFormNext, NextField);
        registry.Register(CommandDef.ServerFormPrevious, PreviousField);
        registry.Register(CommandDef.ServerFormToggle, ToggleField);
        registry.Register(CommandDef.ServerFormApply, ApplyForm);
        registry.Register(CommandDef.ServerFormCancel, CancelForm);
    }
}

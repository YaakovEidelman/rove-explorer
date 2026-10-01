using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ServersViewModel
{
    public const string AddressHint =
        "Type a server address, like sftp://host/folder, smb://nas/share or ftp://host.";

    private SavedServer? _editing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocusSearch), nameof(ShowEmpty), nameof(FocusAddress), nameof(FocusUser), nameof(FocusPassword),
        nameof(FocusName), nameof(FocusRemember), nameof(FocusSavePassword), nameof(OnOption))]
    private bool _inForm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocusAddress), nameof(FocusUser), nameof(FocusPassword), nameof(FocusName),
        nameof(FocusRemember), nameof(FocusSavePassword), nameof(OnOption))]
    private ServerFormField _field;

    [ObservableProperty]
    private string _formTitle = string.Empty;

    [ObservableProperty]
    private string _formAddress = string.Empty;

    [ObservableProperty]
    private string _formUser = string.Empty;

    [ObservableProperty]
    private string _formPassword = string.Empty;

    [ObservableProperty]
    private string _passwordPlaceholder = string.Empty;

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordOptionEnabled))]
    private bool _formRemember;

    [ObservableProperty]
    private bool _formSavePassword;

    public bool FocusAddress => InForm && Field == ServerFormField.Address;

    public bool FocusUser => InForm && Field == ServerFormField.User;

    public bool FocusPassword => InForm && Field == ServerFormField.Password;

    public bool FocusName => InForm && Field == ServerFormField.Name;

    public bool FocusRemember => InForm && Field == ServerFormField.Remember;

    public bool FocusSavePassword => InForm && Field == ServerFormField.SavePassword;

    public bool OnOption => InForm && Field is ServerFormField.Remember or ServerFormField.SavePassword;

    public bool PasswordOptionEnabled => FormRemember && CanSavePasswords;

    partial void OnFormRememberChanged(bool value)
    {
        if (!value)
            FormSavePassword = false;
    }

    private void OpenForm(SavedServer? server)
    {
        AppSettings settings = _settings.Current;
        _editing = server;
        FormTitle = server is null ? "New server" : $"Edit {server.Name}";
        FormAddress = server?.Address ?? "sftp://";
        FormUser = string.Empty;
        FormPassword = string.Empty;
        PasswordPlaceholder = server is { SavePassword: true } ? "saved, leave empty to keep it" : "ask when needed";
        FormName = server?.Name ?? string.Empty;
        FormRemember = server is not null || settings.RememberServers;
        FormSavePassword = server?.SavePassword ?? settings.SavePasswords && FormRemember && CanSavePasswords;
        Field = ServerFormField.Address;
        InForm = true;
    }

    private void CloseForm()
    {
        InForm = false;
        _editing = null;
        FormPassword = string.Empty;
    }

    private void CancelForm() => CloseForm();

    private void NextField() => MoveField(1);

    private void PreviousField() => MoveField(-1);

    private void MoveField(int step)
    {
        ServerFormField[] fields = Fields();
        int index = Array.IndexOf(fields, Field);
        Field = fields[(index + step + fields.Length) % fields.Length];
    }

    private ServerFormField[] Fields() => PasswordOptionEnabled
        ? Enum.GetValues<ServerFormField>()
        : [.. Enum.GetValues<ServerFormField>().Where(f => f != ServerFormField.SavePassword)];

    private void ToggleField()
    {
        if (Field == ServerFormField.Remember)
            FormRemember = !FormRemember;
        else if (Field == ServerFormField.SavePassword && PasswordOptionEnabled)
            FormSavePassword = !FormSavePassword;
    }

    private void ApplyForm()
    {
        string typed = FormAddress.Trim();
        if (!MountAddress.LooksRemote(typed))
        {
            ErrorRaised?.Invoke(typed.Length == 0 || typed.EndsWith("://", StringComparison.Ordinal)
                ? AddressHint
                : $"{typed} isn't a server address. {AddressHint}");
            Field = ServerFormField.Address;
            return;
        }

        string address = MountAddress.WithUser(typed, FormUser.Trim());
        string name = FormName.Trim();
        string password = FormPassword;
        bool remember = FormRemember;
        bool savePassword = remember && FormSavePassword && CanSavePasswords;
        SavedServer? editing = _editing;

        if (editing is not null)
        {
            CloseForm();
            _ = SaveEditAsync(editing, new SavedServer(
                name.Length > 0 ? name : MountAddress.ShortName(address), address, savePassword), password);
            return;
        }

        Close();
        _ = ConnectNewAsync(address, name, password, remember, savePassword);
    }

    private async Task ConnectNewAsync(string address, string name, string password, bool remember, bool savePassword)
    {
        if (await ConnectAsync(address, password.Length > 0 ? password : null) is not { } prompter || !remember)
            return;

        string saved = prompter.TypedUser is { Length: > 0 } user ? MountAddress.WithUser(address, user) : address;
        SavedServer server = new(name.Length > 0 ? name : MountAddress.ShortName(saved), saved, savePassword);
        _store.Save(server);
        if (savePassword && prompter.UsedPassword is { Length: > 0 } used)
            await SavePasswordAsync(server, used);
    }

    private async Task SaveEditAsync(SavedServer old, SavedServer server, string password)
    {
        _store.Save(server, replacing: old.Address);
        InfoRaised?.Invoke($"Saved {server.Name}.");
        if (_secrets is null)
            return;

        bool moved = old.Address != server.Address;
        if (!server.SavePassword)
        {
            if (old.SavePassword)
                await _secrets.DeleteAsync(old.Address, CancellationToken.None);
            return;
        }

        string? keep = password.Length > 0 ? password
            : old.SavePassword && moved ? await _secrets.LookupAsync(old.Address, CancellationToken.None)
            : null;
        if (keep is not null)
            await SavePasswordAsync(server, keep);
        if (moved && old.SavePassword)
            await _secrets.DeleteAsync(old.Address, CancellationToken.None);
    }
}

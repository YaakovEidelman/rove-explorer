using Avalonia.Input;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class ServerKeyTests : HeadlessTest
{
    private static void Fill(string root) => Directory.CreateDirectory(Path.Combine(root, "remote"));

    private sealed record Rig(
        WindowHarness Harness, FakeMountService Mounts, FakeSecretStore Secrets, List<string?> Passwords) : IDisposable
    {
        public void Dispose() => Harness.Dispose();
    }

    private static Rig Open()
    {
        FakeMountService mounts = new();
        FakeSecretStore secrets = new();
        List<string?> passwords = [];
        mounts.OnConnect = async (_, prompter) =>
        {
            passwords.Add(await prompter.AskTextAsync("Password for host", "Password", secret: true, null));
            return CommandResult<string>.Ok(mounts.MountedPath);
        };
        WindowHarness harness = WindowHarness.Open(Fill, mounts: mounts, secrets: secrets);
        mounts.MountedPath = Path.Combine(harness.Root, "remote");
        return new Rig(harness, mounts, secrets, passwords);
    }

    private static void StartNew(WindowHarness harness)
    {
        harness.Press(Key.G, RawInputModifiers.Control);
        harness.Model.Servers.SelectedIndex = 0;
        harness.Press(Key.Enter);
    }

    private static void TabTo(WindowHarness harness, ServerFormField field)
    {
        for (int i = 0; i < 10 && harness.Model.Servers.Field != field; i++)
            harness.Press(Key.Tab);
    }

    [Fact]
    public Task TabMovesFromBookmarksToServersToSettings() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.B, RawInputModifiers.Shift);
        harness.Press(Key.Tab);
        Assert.Equal(Mode.Servers, harness.Model.GetCurrentMode());

        harness.Press(Key.Tab);
        Assert.Equal(Mode.Settings, harness.Model.GetCurrentMode());
        Assert.False(harness.Model.Servers.IsOpen);
    });

    [Fact]
    public Task ANewServerConnectsWithTheFormAndIsRemembered() => OnUiThread(() =>
    {
        using Rig rig = Open();
        (WindowHarness harness, FakeMountService mounts, FakeSecretStore secrets, List<string?> passwords) = rig;

        StartNew(harness);
        Assert.Equal(Mode.ServerForm, harness.Model.GetCurrentMode());
        Assert.Equal("sftp://", harness.Model.Servers.FormAddress);
        harness.Model.Servers.FormAddress = "sftp://host/srv";
        harness.Model.Servers.FormUser = "me";
        harness.Model.Servers.FormPassword = "hunter2";
        harness.Press(Key.Enter);
        harness.Settle();

        Assert.Contains("connect sftp://me@host/srv", mounts.Calls);
        Assert.Equal(["hunter2"], passwords);
        Assert.Equal(mounts.MountedPath, harness.Content.DirectoryListing.CurrentDir);
        SavedServer saved = Assert.Single(harness.SavedServers.Items);
        Assert.Equal(new SavedServer("me@host", "sftp://me@host/srv", SavePassword: false), saved);
        Assert.Empty(secrets.Saved);
    });

    [Fact]
    public Task TheSavePasswordSwitchPutsThePasswordInTheKeyring() => OnUiThread(() =>
    {
        using Rig rig = Open();
        (WindowHarness harness, FakeSecretStore secrets) = (rig.Harness, rig.Secrets);

        StartNew(harness);
        harness.Model.Servers.FormAddress = "sftp://me@host/";
        harness.Model.Servers.FormPassword = "hunter2";
        TabTo(harness, ServerFormField.SavePassword);
        Assert.Equal(Mode.ServerFormOption, harness.Model.GetCurrentMode());
        harness.Press(Key.Space);
        Assert.True(harness.Model.Servers.FormSavePassword);
        harness.Press(Key.Enter);
        harness.Settle();

        Assert.True(Assert.Single(harness.SavedServers.Items).SavePassword);
        Assert.Equal("hunter2", secrets.Saved["sftp://me@host/"]);
    });

    [Fact]
    public Task TurningRememberOffSavesNothing() => OnUiThread(() =>
    {
        using Rig rig = Open();
        (WindowHarness harness, FakeMountService mounts) = (rig.Harness, rig.Mounts);

        StartNew(harness);
        harness.Model.Servers.FormAddress = "smb://nas/share";
        TabTo(harness, ServerFormField.Remember);
        harness.Press(Key.Space);
        harness.Press(Key.Enter);
        harness.Settle();

        Assert.Contains("connect smb://nas/share", mounts.Calls);
        Assert.Empty(harness.SavedServers.Items);
    });

    [Fact]
    public Task TheSettingsDecideHowTheSwitchesStart() => OnUiThread(() =>
    {
        using Rig rig = Open();
        WindowHarness harness = rig.Harness;
        SettingsViewModel settings = harness.Model.Settings;
        harness.Press(Key.OemComma);
        while (settings.Sections[settings.SelectedSectionIndex] != "Servers")
            settings.NextSection();
        settings.SelectedIndex = settings.Rows.IndexOf(settings.Rows.Single(r => r.Kind == SettingsRowKind.SavePasswords));
        settings.Activate();
        harness.Press(Key.Escape);

        StartNew(harness);

        Assert.True(harness.Model.Servers.FormRemember);
        Assert.True(harness.Model.Servers.FormSavePassword);
    });

    [Fact]
    public Task ASavedServerUsesItsSavedPassword() => OnUiThread(() =>
    {
        using Rig rig = Open();
        (WindowHarness harness, FakeMountService mounts, FakeSecretStore secrets, List<string?> passwords) = rig;
        harness.SavedServers.Save(new SavedServer("work", "sftp://me@work/", SavePassword: true));
        secrets.Saved["sftp://me@work/"] = "s3cret";

        harness.Press(Key.G, RawInputModifiers.Control);
        Assert.Equal("work", harness.Model.Servers.Items[harness.Model.Servers.SelectedIndex].Title);
        harness.Press(Key.Enter);
        harness.Settle();

        Assert.Contains("connect sftp://me@work/", mounts.Calls);
        Assert.Equal(["s3cret"], passwords);
        Assert.False(harness.Model.Prompt.IsOpen);
    });

    [Fact]
    public Task ForgettingAServerDropsItAndItsPassword() => OnUiThread(() =>
    {
        using Rig rig = Open();
        (WindowHarness harness, FakeSecretStore secrets) = (rig.Harness, rig.Secrets);
        harness.SavedServers.Save(new SavedServer("work", "sftp://me@work/", SavePassword: true));
        secrets.Saved["sftp://me@work/"] = "s3cret";

        harness.Press(Key.G, RawInputModifiers.Control);
        harness.Press(Key.D, RawInputModifiers.Control);
        harness.Settle();

        Assert.Empty(harness.SavedServers.Items);
        Assert.Empty(secrets.Saved);
        Assert.True(harness.Model.Servers.IsEmpty);
    });

    [Fact]
    public Task EditingRenamesWithoutConnecting() => OnUiThread(() =>
    {
        using Rig rig = Open();
        (WindowHarness harness, FakeMountService mounts) = (rig.Harness, rig.Mounts);
        harness.SavedServers.Save(new SavedServer("work", "sftp://me@work/", SavePassword: false));

        harness.Press(Key.G, RawInputModifiers.Control);
        harness.Press(Key.E, RawInputModifiers.Control);
        Assert.Equal("sftp://me@work/", harness.Model.Servers.FormAddress);
        harness.Model.Servers.FormName = "office";
        harness.Press(Key.Enter);
        harness.Settle();

        Assert.Equal("office", Assert.Single(harness.SavedServers.Items).Name);
        Assert.DoesNotContain(mounts.Calls, call => call.StartsWith("connect", StringComparison.Ordinal));
        Assert.Equal(Mode.Servers, harness.Model.GetCurrentMode());
    });

    [Fact]
    public Task SomethingThatIsNotAnAddressKeepsTheFormOpen() => OnUiThread(() =>
    {
        using Rig rig = Open();
        (WindowHarness harness, FakeMountService mounts) = (rig.Harness, rig.Mounts);

        StartNew(harness);
        harness.Model.Servers.FormAddress = "/home/me";
        harness.Press(Key.Enter);

        Assert.True(harness.Model.Servers.InForm);
        Assert.Contains("isn't a server address", harness.Model.StatusError);
        Assert.DoesNotContain(mounts.Calls, call => call.StartsWith("connect", StringComparison.Ordinal));
    });

    [Fact]
    public Task EscLeavesTheFormButKeepsTheList() => OnUiThread(() =>
    {
        using Rig rig = Open();
        WindowHarness harness = rig.Harness;

        StartNew(harness);
        harness.Press(Key.Escape);

        Assert.False(harness.Model.Servers.InForm);
        Assert.Equal(Mode.Servers, harness.Model.GetCurrentMode());
    });
}

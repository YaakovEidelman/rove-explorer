using Avalonia.Input;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class MountKeyTests : HeadlessTest
{
    private static void Fill(string root) => Directory.CreateDirectory(Path.Combine(root, "remote"));

    [Fact]
    public Task TypingAServerAddressInThePathBarConnectsAndGoesThere() => OnUiThread(() =>
    {
        FakeMountService mounts = new();
        using WindowHarness harness = WindowHarness.Open(Fill, mounts: mounts);
        mounts.MountedPath = Path.Combine(harness.Root, "remote");

        harness.Press(Key.L, RawInputModifiers.Control);
        harness.Content.EditPathText = "sftp://me@host/srv";
        harness.Press(Key.Enter);
        harness.Settle();

        Assert.Contains("connect sftp://me@host/srv", mounts.Calls);
        Assert.Equal(mounts.MountedPath, harness.Content.DirectoryListing.CurrentDir);
        Assert.False(harness.Content.InEditPath);
    });

    [Fact]
    public Task ADriveNamedAfterYouWentInStillRenamesThePathBarAndTab() => OnUiThread(() =>
    {
        FakeMountService mounts = new();
        using WindowHarness harness = WindowHarness.Open(Fill, mounts: mounts);
        string stick = Path.Combine(harness.Root, "remote");
        harness.GoTo(stick);

        mounts.Entries.Add(new("STICK", MountKind.Removable, "/dev/sdb1", null, new Uri(stick).AbsoluteUri, stick,
            CanMount: false, CanUnmount: true, CanEject: true));
        mounts.RaiseChanged();
        for (int i = 0; i < 20 && harness.Content.DirectoryListing.Crumbs[^1].Label != "USB Drive STICK"; i++)
        {
            Thread.Sleep(10);
            harness.Settle();
        }

        Assert.Equal("USB Drive STICK", harness.Content.DirectoryListing.Crumbs[^1].Label);
        Assert.Equal("USB Drive STICK", harness.Tabs.Items[0].Title);
    });

    [Fact]
    public Task CtrlGAsksForAServerAndEscCancels() => OnUiThread(() =>
    {
        FakeMountService mounts = new();
        using WindowHarness harness = WindowHarness.Open(Fill, mounts: mounts);

        harness.Press(Key.G, RawInputModifiers.Control);

        Assert.True(harness.Model.Prompt.IsOpen);
        Assert.Equal(Mode.Prompt, harness.Model.GetCurrentMode());
        Assert.Equal("sftp://", harness.Model.Prompt.Text);

        harness.Press(Key.Escape);

        Assert.False(harness.Model.Prompt.IsOpen);
        Assert.DoesNotContain(mounts.Calls, call => call.StartsWith("connect", StringComparison.Ordinal));
    });

    [Fact]
    public Task APasswordQuestionIsAnsweredFromThePromptBox() => OnUiThread(() =>
    {
        FakeMountService mounts = new();
        string? given = null;
        mounts.OnConnect = async (address, prompter) =>
        {
            given = await prompter.AskTextAsync("Enter password for me on host", "Password", secret: true, null);
            return given is null
                ? CommandResult<string>.Fail("aborted", "Cancelled.")
                : CommandResult<string>.Ok(mounts.MountedPath);
        };
        using WindowHarness harness = WindowHarness.Open(Fill, mounts: mounts);
        mounts.MountedPath = Path.Combine(harness.Root, "remote");

        harness.Press(Key.L, RawInputModifiers.Control);
        harness.Content.EditPathText = "sftp://me@host/";
        harness.Press(Key.Enter);

        Assert.True(harness.Model.Prompt.IsOpen);
        Assert.True(harness.Model.Prompt.IsSecret);
        harness.Model.Prompt.Text = "hunter2";
        harness.Press(Key.Enter);
        harness.Settle();

        Assert.Equal("hunter2", given);
        Assert.False(harness.Model.Prompt.IsOpen);
        Assert.Equal(mounts.MountedPath, harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task WithoutMountSupportAServerAddressExplainsWhy() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.L, RawInputModifiers.Control);
        harness.Content.EditPathText = "smb://nas/share";
        harness.Press(Key.Enter);

        Assert.Contains("gio", harness.Model.StatusError);
    });

    [Fact]
    public Task RefreshingTheDriveListKeepsServerCommands() => OnUiThread(() =>
    {
        FakeMountService mounts = new();
        mounts.Entries.Add(new MountEntry(
            "nas", MountKind.Network, null, null, "smb://nas/share/", "/run/user/1000/gvfs/nas",
            CanMount: false, CanUnmount: true, CanEject: false));
        using WindowHarness harness = WindowHarness.Open(Fill, mounts: mounts);

        harness.Content.RefreshDriveCommands();

        string[] ids = [.. harness.Registry.CommandIdsStartingWith(CommandDef.MountIdPrefix)];
        Assert.Equal(2, ids.Length);
        Assert.NotEmpty(harness.Registry.CommandIdsStartingWith(CommandDef.DriveIdPrefix).Except(ids));
    });
}

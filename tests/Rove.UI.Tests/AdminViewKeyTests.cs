using Avalonia.Input;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.UI.Tests;

public class AdminViewKeyTests : HeadlessTest
{
    private sealed class FakeAdminSession : IAdminSession
    {
        public List<string> Requests { get; } = [];
        public bool Refuse { get; init; }
        public bool IsRunning { get; private set; }

        public Task<CommandResult<FolderItem[]>> ReadDirectoryAsync(string path)
        {
            Requests.Add(path);
            if (Refuse)
                return Task.FromResult(
                    CommandResult<FolderItem[]>.Fail("admin_failed", "Administrator access was cancelled."));
            IsRunning = true;
            FolderItem[] items =
            [
                new("secret.txt", Path.Combine(path, "secret.txt"), FileAttributes.Normal,
                    DateTime.Now, false, 3, ".txt"),
            ];
            return Task.FromResult(CommandResult<FolderItem[]>.Ok(items));
        }

        public void Dispose()
        {
        }
    }

    private static void FillWithLockedFolder(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "locked"));
        File.WriteAllText(Path.Combine(root, "open.txt"), "");
    }

    private static void Lock(string root) =>
        File.SetUnixFileMode(Path.Combine(root, "locked"), UnixFileMode.None);

    private static void Unlock(string root) =>
        File.SetUnixFileMode(Path.Combine(root, "locked"), UnixFileMode.UserRead | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);

    [Fact]
    public Task EnteringALockedFolderAsksBeforeUsingAdministratorAccess() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");

            harness.Press(Key.Enter);

            Assert.True(harness.Model.Confirm.IsOpen);
            Assert.Empty(admin.Requests);
            Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task AcceptingOpensTheFolderThroughTheAdministratorSession() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);

            harness.Press(Key.Y);

            string locked = Path.Combine(harness.Root, "locked");
            Assert.Equal([locked], admin.Requests);
            Assert.Equal(locked, harness.Content.DirectoryListing.CurrentDir);
            Assert.Equal(["secret.txt"], harness.Names());
            Assert.True(harness.Content.IsAdminView);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task DecliningLeavesEverythingAsItWas() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);

            harness.Press(Key.N);

            Assert.Empty(admin.Requests);
            Assert.False(harness.Model.Confirm.IsOpen);
            Assert.False(harness.Content.IsAdminView);
            Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task OnceUnlockedTheNextLockedFolderOpensWithoutAskingAgain() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);
            harness.Press(Key.Y);
            harness.Press(Key.H);
            Assert.False(harness.Content.IsAdminView);
            Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);

            harness.Highlight("locked");
            harness.Press(Key.Enter);

            Assert.False(harness.Model.Confirm.IsOpen);
            Assert.True(harness.Content.IsAdminView);
            Assert.Equal(2, admin.Requests.Count);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task ARefusedPasswordShowsTheReasonAndStaysPut() => OnUiThread(() =>
    {
        FakeAdminSession admin = new() { Refuse = true };
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);

            harness.Press(Key.Y);

            Assert.Equal("Administrator access was cancelled.", harness.Model.StatusError);
            Assert.False(harness.Model.Confirm.IsOpen);
            Assert.False(harness.Content.IsAdminView);
            Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task WithoutAdministratorSupportALockedFolderJustShowsTheError() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");

            harness.Press(Key.Enter);

            Assert.False(harness.Model.Confirm.IsOpen);
            Assert.Contains("Access denied", harness.Model.StatusError);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task WritingVerbsAreRefusedInAdministratorView() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);
            harness.Press(Key.Y);
            harness.Highlight("secret.txt");

            harness.Press(Key.D, RawInputModifiers.Shift);

            Assert.False(harness.Model.Confirm.IsOpen);
            Assert.Contains("administrator view", harness.Model.StatusLine);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });
}

using Avalonia.Input;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class AdminViewKeyTests : HeadlessTest
{
    private sealed class FakeAdminSession : IAdminSession
    {
        public List<string> Requests { get; } = [];
        public bool Refuse { get; init; }
        public bool IsRunning { get; private set; }
        public List<(string Path, long MaxBytes)> CopyRequests { get; } = [];
        public List<string> Discarded { get; } = [];
        public string CopyContent { get; init; } = "hello from root";
        public bool CopyFails { get; init; }

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

        public Task<CommandResult<string>> CopyToTempAsync(
            string path, long maxBytes, CancellationToken ct = default)
        {
            CopyRequests.Add((path, maxBytes));
            if (CopyFails)
                return Task.FromResult(CommandResult<string>.Fail("not_a_file", "Could not read it."));
            string folder = Directory.CreateTempSubdirectory("rove-fake-admin-").FullName;
            string copy = Path.Combine(folder, Path.GetFileName(path));
            File.WriteAllText(copy, CopyContent);
            return Task.FromResult(CommandResult<string>.Ok(copy));
        }

        public void Discard(string copy)
        {
            Discarded.Add(copy);
            Directory.Delete(Path.GetDirectoryName(copy)!, recursive: true);
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
    public Task EnteringALockedFolderGoesStraightToTheAdministratorSession() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");

            harness.Press(Key.Enter);

            string locked = Path.Combine(harness.Root, "locked");
            Assert.False(harness.Model.Confirm.IsOpen);
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
    public Task TheTabIsMarkedWhileItShowsTheAdministratorView() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            FolderTab tab = harness.Model.Tabs.Items[harness.Model.Tabs.ActiveIndex];
            Assert.False(tab.IsAdmin);
            harness.Highlight("locked");

            harness.Press(Key.Enter);
            Assert.True(tab.IsAdmin);
            Assert.DoesNotContain("administrator", harness.Model.ItemSummary);

            harness.Press(Key.H);
            Assert.False(tab.IsAdmin);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task TheSessionIsReusedForTheNextLockedFolder() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);
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

    [Fact]
    public Task PreviewShowsTheStartOfTheFileFromACopyAndDiscardsIt() => OnUiThread(() =>
    {
        FakeAdminSession admin = new();
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);
            harness.Model.Preview.Toggle();

            harness.Highlight("secret.txt");
            harness.Settle();

            string secret = Path.Combine(harness.Root, "locked", "secret.txt");
            Assert.Equal("hello from root", harness.Model.Preview.PreviewText);
            Assert.Contains(harness.Model.Preview.Rows, r => r.Label == "Location" && r.Value == secret);
            Assert.All(admin.CopyRequests, r => Assert.Equal((secret, 16 * 1024L), r));
            Assert.NotEmpty(admin.CopyRequests);
            Assert.Equal(admin.CopyRequests.Count, admin.Discarded.Count);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task PreviewSaysWhyWhenTheCopyFails() => OnUiThread(() =>
    {
        FakeAdminSession admin = new() { CopyFails = true };
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);
            harness.Model.Preview.Toggle();

            harness.Highlight("secret.txt");
            harness.Settle();

            Assert.False(harness.Model.Preview.HasTextPreview);
            Assert.Contains(harness.Model.Preview.Rows, r => r.Label == "Error");
        }
        finally
        {
            Unlock(harness.Root);
        }
    });

    [Fact]
    public Task OpeningAFileAsksTheSessionForTheWholeFileAndSaysSo() => OnUiThread(() =>
    {
        FakeAdminSession admin = new() { CopyFails = true };
        using WindowHarness harness = WindowHarness.Open(FillWithLockedFolder, admin);
        Lock(harness.Root);
        try
        {
            harness.Highlight("locked");
            harness.Press(Key.Enter);
            harness.Highlight("secret.txt");

            harness.Press(Key.Enter);
            harness.Settle();

            string secret = Path.Combine(harness.Root, "locked", "secret.txt");
            Assert.Equal([(secret, long.MaxValue)], admin.CopyRequests);
            Assert.Equal("Could not read it.", harness.Model.StatusError);
        }
        finally
        {
            Unlock(harness.Root);
        }
    });
}

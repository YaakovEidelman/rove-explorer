using System.Diagnostics;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class LinuxAdminSessionTests
{
    private static LinuxAdminSession Open() =>
        new("/usr/bin/bash", ["-c", LinuxAdminSession.Script]);

    [Fact]
    public async Task ListsFilesAndFoldersWithTheirDetails()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "report.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(dir.Path, "Photos"));
        using LinuxAdminSession session = Open();

        CommandResult<FolderItem[]> result = await session.ReadDirectoryAsync(dir.Path);

        Assert.True(result.IsOk);
        FolderItem file = Assert.Single(result.Data!, i => i.Name == "report.txt");
        Assert.False(file.IsDirectory);
        Assert.Equal(5, file.Size);
        Assert.Equal(".txt", file.Extension);
        Assert.Equal(Path.Combine(dir.Path, "report.txt"), file.FullPath);
        FolderItem folder = Assert.Single(result.Data!, i => i.Name == "Photos");
        Assert.True(folder.IsDirectory);
        Assert.Null(folder.Size);
    }

    [Fact]
    public async Task KeepsNamesWithTabsNewlinesAndSpacesIntact()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        string[] names = ["two\nlines.txt", "tab\there.txt", "  spaced  .txt", "üñí.txt"];
        foreach (string name in names)
            File.WriteAllText(Path.Combine(dir.Path, name), "");
        using LinuxAdminSession session = Open();

        CommandResult<FolderItem[]> result = await session.ReadDirectoryAsync(dir.Path);

        Assert.Equal(
            [.. names.OrderBy(n => n, StringComparer.Ordinal)],
            [.. result.Data!.Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task AFolderNamedLikeTheEndMarkerIsStillAnEntry()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "END"));
        using LinuxAdminSession session = Open();

        CommandResult<FolderItem[]> result = await session.ReadDirectoryAsync(dir.Path);

        Assert.Equal(["END"], result.Data!.Select(i => i.Name));
    }

    [Fact]
    public async Task MarksSymlinksAndHiddenFiles()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "real"));
        File.CreateSymbolicLink(Path.Combine(dir.Path, "link"), Path.Combine(dir.Path, "real"));
        File.WriteAllText(Path.Combine(dir.Path, ".secret"), "");
        using LinuxAdminSession session = Open();

        CommandResult<FolderItem[]> result = await session.ReadDirectoryAsync(dir.Path);

        FolderItem link = Assert.Single(result.Data!, i => i.Name == "link");
        Assert.True(link.IsDirectory);
        Assert.True(link.Attributes.HasFlag(FileAttributes.ReparsePoint));
        FolderItem hidden = Assert.Single(result.Data!, i => i.Name == ".secret");
        Assert.True(hidden.Attributes.HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    public async Task ServesSeveralFoldersOnOneSession()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "a"));
        File.WriteAllText(Path.Combine(dir.Path, "a", "inner.txt"), "");
        using LinuxAdminSession session = Open();

        CommandResult<FolderItem[]> top = await session.ReadDirectoryAsync(dir.Path);
        CommandResult<FolderItem[]> inner = await session.ReadDirectoryAsync(Path.Combine(dir.Path, "a"));
        CommandResult<FolderItem[]> topAgain = await session.ReadDirectoryAsync(dir.Path);

        Assert.Equal(["a"], top.Data!.Select(i => i.Name));
        Assert.Equal(["inner.txt"], inner.Data!.Select(i => i.Name));
        Assert.Equal(["a"], topAgain.Data!.Select(i => i.Name));
        Assert.True(session.IsRunning);
    }

    [Fact]
    public async Task AFolderThatCannotBeReadFailsWithoutStoppingTheSession()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        using LinuxAdminSession session = Open();

        CommandResult<FolderItem[]> missing =
            await session.ReadDirectoryAsync(Path.Combine(dir.Path, "nope"));
        CommandResult<FolderItem[]> fine = await session.ReadDirectoryAsync(dir.Path);

        Assert.False(missing.IsOk);
        Assert.Equal("permission_denied", missing.Reason);
        Assert.True(fine.IsOk);
    }

    [Fact]
    public async Task ARelativePathIsRefusedBeforeAnythingStarts()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using LinuxAdminSession session = Open();

        CommandResult<FolderItem[]> result = await session.ReadDirectoryAsync("relative/dir");

        Assert.Equal("bad_path", result.Reason);
        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task ALauncherThatFailsReportsWhyAndCanBeTriedAgain()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        using LinuxAdminSession session = new("/usr/bin/bash", ["-c", "exit 126"]);

        CommandResult<FolderItem[]> result = await session.ReadDirectoryAsync(dir.Path);

        Assert.Equal("admin_failed", result.Reason);
        Assert.Equal("Administrator access was cancelled.", result.Message);
        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task CopiesAFileByteForByteIntoAPrivateFolder()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        byte[] content = new byte[300_000];
        new Random(7).NextBytes(content);
        string source = Path.Combine(dir.Path, "data.bin");
        File.WriteAllBytes(source, content);
        using LinuxAdminSession session = Open();

        CommandResult<string> result = await session.CopyToTempAsync(source, long.MaxValue);

        Assert.True(result.IsOk);
        Assert.Equal("data.bin", Path.GetFileName(result.Data));
        Assert.Equal(content, File.ReadAllBytes(result.Data!));
        Assert.Equal(UnixFileMode.UserRead, File.GetUnixFileMode(result.Data!));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(Path.GetDirectoryName(result.Data!)!));
    }

    [Fact]
    public async Task CopiesOnlyAsMuchAsAskedFor()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        string source = Path.Combine(dir.Path, "long.txt");
        File.WriteAllText(source, "0123456789abcdef");
        using LinuxAdminSession session = Open();

        CommandResult<string> result = await session.CopyToTempAsync(source, 10);

        Assert.Equal("0123456789", File.ReadAllText(result.Data!));
    }

    [Fact]
    public async Task EveryLengthOfCopyComesBackExact()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        byte[] content = Enumerable.Range(0, 40).Select(i => (byte)(i * 7)).ToArray();
        string source = Path.Combine(dir.Path, "run.bin");
        File.WriteAllBytes(source, content);
        using LinuxAdminSession session = Open();

        for (int length = 0; length <= content.Length; length++)
        {
            CommandResult<string> result = await session.CopyToTempAsync(source, length);
            Assert.Equal(content[..length], File.ReadAllBytes(result.Data!));
        }
    }

    [Fact]
    public async Task CopiesAnEmptyFileAndKeepsTheSessionInStep()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        string empty = Path.Combine(dir.Path, "empty");
        File.WriteAllText(empty, "");
        using LinuxAdminSession session = Open();

        CommandResult<string> copy = await session.CopyToTempAsync(empty, long.MaxValue);
        CommandResult<FolderItem[]> listing = await session.ReadDirectoryAsync(dir.Path);

        Assert.Empty(File.ReadAllBytes(copy.Data!));
        Assert.Equal(["empty"], listing.Data!.Select(i => i.Name));
    }

    [Fact]
    public async Task AFolderOrAMissingFileIsNotCopiedAndTheSessionCarriesOn()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        using LinuxAdminSession session = Open();

        CommandResult<string> folder = await session.CopyToTempAsync(dir.Path, long.MaxValue);
        CommandResult<string> missing =
            await session.CopyToTempAsync(Path.Combine(dir.Path, "nope"), long.MaxValue);
        CommandResult<FolderItem[]> listing = await session.ReadDirectoryAsync(dir.Path);

        Assert.Equal("not_a_file", folder.Reason);
        Assert.Equal("not_a_file", missing.Reason);
        Assert.True(listing.IsOk);
    }

    [Fact]
    public async Task ARelativePathIsNotCopied()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using LinuxAdminSession session = Open();

        CommandResult<string> result = await session.CopyToTempAsync("relative.txt", long.MaxValue);

        Assert.Equal("bad_path", result.Reason);
        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task DiscardRemovesJustThatCopyAndDisposeRemovesTheRest()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        string source = Path.Combine(dir.Path, "a.txt");
        File.WriteAllText(source, "a");
        LinuxAdminSession session = Open();
        string first = (await session.CopyToTempAsync(source, long.MaxValue)).Data!;
        string second = (await session.CopyToTempAsync(source, long.MaxValue)).Data!;

        session.Discard(first);

        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));
        session.Dispose();
        Assert.False(File.Exists(second));
    }

    [Fact]
    public async Task DiscardLeavesAlonePathsThatAreNotItsCopies()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using TempDir dir = new();
        string mine = Path.Combine(dir.Path, "keep.txt");
        File.WriteAllText(mine, "keep");
        using LinuxAdminSession session = Open();
        await session.CopyToTempAsync(mine, long.MaxValue);

        session.Discard(mine);

        Assert.True(File.Exists(mine));
    }

    [Theory]
    [InlineData("wrong-secret", "list", "/tmp", "")]
    [InlineData(null, "delete", "/tmp", "")]
    [InlineData(null, "read", "/etc/hostname", "-1")]
    [InlineData(null, "list", "relative", "")]
    public async Task TheHelperStopsOnARequestItShouldNotHaveGot(
        string? token, string verb, string path, string limit)
    {
        if (!OperatingSystem.IsLinux())
            return;
        const string secret = "s3cret";
        ProcessStartInfo start = new("/usr/bin/bash")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(LinuxAdminSession.Script);
        using Process helper = Process.Start(start)!;

        string request = $"{secret}\0{token ?? secret}\0{verb}\0{path}\0{limit}\0";
        await helper.StandardInput.WriteAsync(request);
        await helper.StandardInput.FlushAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await helper.WaitForExitAsync(timeout.Token);

        Assert.Equal(1, helper.ExitCode);
        Assert.Equal("", await helper.StandardOutput.ReadToEndAsync());
    }
}

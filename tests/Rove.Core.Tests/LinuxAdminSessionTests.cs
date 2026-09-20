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
}

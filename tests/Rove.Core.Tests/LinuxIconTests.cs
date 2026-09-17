using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

/// <summary>
/// Shares the process-wide XDG environment variables, so these classes take
/// turns instead of overwriting each other's scratch directories.
/// </summary>
public sealed class XdgEnvironment
{
    public const string Name = "xdg environment";
}

[CollectionDefinition(XdgEnvironment.Name, DisableParallelization = true)]
[Collection(XdgEnvironment.Name)]
public class LinuxIconTests
{
    private sealed class FakeXdg : IDisposable
    {
        private readonly string? _dataHome;
        private readonly string? _dataDirs;
        private readonly string? _configHome;
        private readonly string? _theme;

        public TempDir Root { get; } = new();

        public FakeXdg()
        {
            _dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            _dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            _configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            _theme = Environment.GetEnvironmentVariable("ROVE_ICON_THEME");

            Directory.CreateDirectory(Root.Sub("data"));
            Directory.CreateDirectory(Root.Sub("config"));
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", Root.Sub("data"));
            Environment.SetEnvironmentVariable("XDG_DATA_DIRS", Root.Sub("data"));
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Root.Sub("config"));
            Environment.SetEnvironmentVariable("ROVE_ICON_THEME", null);
        }

        public void Write(string relativePath, string content)
        {
            string full = Root.Sub(relativePath.Split('/'));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        public string Path_(string relativePath) => Normalize(Root.Sub(relativePath.Split('/')));

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", _dataHome);
            Environment.SetEnvironmentVariable("XDG_DATA_DIRS", _dataDirs);
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _configHome);
            Environment.SetEnvironmentVariable("ROVE_ICON_THEME", _theme);
            Root.Dispose();
        }
    }

    private static void WriteTheme(FakeXdg xdg, string name, string inherits = "")
    {
        xdg.Write(
            $"data/icons/{name}/index.theme",
            $"""
            [Icon Theme]
            Name={name}
            Inherits={inherits}
            Directories=16x16/mimetypes,16x16/places,32x32/mimetypes,scalable/mimetypes

            [16x16/mimetypes]
            Size=16
            Type=Fixed

            [16x16/places]
            Size=16
            Type=Fixed

            [32x32/mimetypes]
            Size=32
            Type=Fixed

            [scalable/mimetypes]
            Size=16
            MinSize=8
            MaxSize=512
            Type=Scalable
            """
        );
    }

    private static void WriteMime(FakeXdg xdg)
    {
        xdg.Write("data/mime/globs2", "#comment\n50:text/plain:*.txt\n50:image/png:*.png\n");
        xdg.Write("data/mime/generic-icons", "text/plain:text-x-generic\n");
    }

    private static FolderItem File_(string name, string extension) =>
        new(name, "/tmp/" + name, FileAttributes.Normal, DateTime.UnixEpoch, false, 1, extension);

    private static FolderItem Dir_(string name) =>
        new(name, "/tmp/" + name, FileAttributes.Directory, DateTime.UnixEpoch, true, null, "");

    [Fact]
    public void MimeDatabase_MapsExtensionToTypeAndGenericIcon()
    {
        using FakeXdg xdg = new();
        WriteMime(xdg);

        LinuxMimeDatabase db = LinuxMimeDatabase.Load();

        Assert.Equal("text/plain", db.MimeForExtension(".txt"));
        Assert.Equal("text/plain", db.MimeForExtension("TXT"));
        Assert.Equal("image/png", db.MimeForExtension(".png"));
        Assert.Null(db.MimeForExtension(".nope"));
        Assert.Equal("text-x-generic", db.GenericIcon("text/plain"));
    }

    [Fact]
    public void MimeDatabase_ResolvesExtensionsForExactAndWildcardMimePatterns()
    {
        using FakeXdg xdg = new();
        WriteMime(xdg);

        LinuxMimeDatabase db = LinuxMimeDatabase.Load();

        Assert.Equal(["txt"], db.ExtensionsForMimePattern("text/plain"));
        Assert.Equal(["png"], db.ExtensionsForMimePattern("image/*"));
        Assert.Empty(db.ExtensionsForMimePattern("image/jpeg"));
    }

    [Fact]
    public void Theme_PrefersExactSizeDirectory()
    {
        using FakeXdg xdg = new();
        WriteTheme(xdg, "Test");
        Environment.SetEnvironmentVariable("ROVE_ICON_THEME", "Test");
        xdg.Write("data/icons/Test/16x16/mimetypes/text-plain.png", "small");
        xdg.Write("data/icons/Test/32x32/mimetypes/text-plain.png", "large");

        Assert.Equal(
            xdg.Path_("data/icons/Test/16x16/mimetypes/text-plain.png"),
            Found(LinuxIconTheme.Load(), "text-plain", 16)
        );
    }

    [Fact]
    public void Theme_FallsBackToNearestSizeThenToInheritedTheme()
    {
        using FakeXdg xdg = new();
        WriteTheme(xdg, "Test", inherits: "Parent");
        WriteTheme(xdg, "Parent");
        Environment.SetEnvironmentVariable("ROVE_ICON_THEME", "Test");
        xdg.Write("data/icons/Test/32x32/mimetypes/text-plain.png", "only-large");
        xdg.Write("data/icons/Parent/16x16/places/folder.png", "parent-folder");

        LinuxIconTheme theme = LinuxIconTheme.Load();

        Assert.Equal(
            xdg.Path_("data/icons/Test/32x32/mimetypes/text-plain.png"),
            Found(theme, "text-plain", 16)
        );
        Assert.Equal(
            xdg.Path_("data/icons/Parent/16x16/places/folder.png"),
            Found(theme, "folder", 16)
        );
        Assert.Null(Found(theme, "nothing-here", 16));
    }

    [Fact]
    public void Theme_AcceptsScalableSvg()
    {
        using FakeXdg xdg = new();
        WriteTheme(xdg, "Test");
        Environment.SetEnvironmentVariable("ROVE_ICON_THEME", "Test");
        xdg.Write("data/icons/Test/scalable/mimetypes/text-plain.svg", "<svg/>");

        Assert.Equal(
            xdg.Path_("data/icons/Test/scalable/mimetypes/text-plain.svg"),
            Found(LinuxIconTheme.Load(), "text-plain", 16)
        );
    }

    [Fact]
    public void Theme_ReadsNameFromGtkSettings()
    {
        using FakeXdg xdg = new();
        WriteTheme(xdg, "FromGtk");
        xdg.Write("config/gtk-3.0/settings.ini", "[Settings]\ngtk-icon-theme-name=FromGtk\n");
        xdg.Write("data/icons/FromGtk/16x16/places/folder.png", "gtk-folder");

        Assert.Equal(
            xdg.Path_("data/icons/FromGtk/16x16/places/folder.png"),
            Found(LinuxIconTheme.Load(), "folder", 16)
        );
    }

    [Fact]
    public async Task Fetcher_ResolvesMimeIconForFilesAndFolderIconForDirectories()
    {
        using FakeXdg xdg = new();
        WriteMime(xdg);
        WriteTheme(xdg, "Test");
        Environment.SetEnvironmentVariable("ROVE_ICON_THEME", "Test");
        xdg.Write("data/icons/Test/16x16/mimetypes/text-plain.png", "text-icon");
        xdg.Write("data/icons/Test/16x16/places/folder.png", "folder-icon");

        LinuxIconFetcher fetcher = new();

        Assert.Equal("text-icon", Text(await fetcher.GetIconAsync(File_("a.txt", ".txt"), 16)));
        Assert.Equal("folder-icon", Text(await fetcher.GetIconAsync(Dir_("stuff"), 16)));
    }

    [Fact]
    public async Task Fetcher_FallsBackToGenericIconWhenTypeIconIsMissing()
    {
        using FakeXdg xdg = new();
        WriteMime(xdg);
        WriteTheme(xdg, "Test");
        Environment.SetEnvironmentVariable("ROVE_ICON_THEME", "Test");
        xdg.Write("data/icons/Test/16x16/mimetypes/text-x-generic.png", "generic-text");
        xdg.Write("data/icons/Test/16x16/mimetypes/application-x-generic.png", "generic-any");

        LinuxIconFetcher fetcher = new();

        Assert.Equal("generic-text", Text(await fetcher.GetIconAsync(File_("a.txt", ".txt"), 16)));
        Assert.Equal("generic-any", Text(await fetcher.GetIconAsync(File_("a.zzz", ".zzz"), 16)));
    }

    [Fact]
    public async Task Fetcher_ReturnsNullWhenNoThemeIsInstalled()
    {
        using FakeXdg xdg = new();
        Environment.SetEnvironmentVariable("ROVE_ICON_THEME", "Missing");

        Assert.Null(await new LinuxIconFetcher().GetIconAsync(File_("a.txt", ".txt"), 16));
    }

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);

    private static string? Found(LinuxIconTheme theme, string name, int size) =>
        theme.FindIconFile(name, size) is { } path ? Normalize(path) : null;

    private static string? Text(byte[]? bytes) =>
        bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
}

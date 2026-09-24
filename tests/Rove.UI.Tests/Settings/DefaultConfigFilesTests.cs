using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class DefaultConfigFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rove-defaults-" + Guid.NewGuid().ToString("N"));

    private string Keybindings => Path.Combine(_dir, "keybindings.json");

    private string Bookmarks => Path.Combine(_dir, "bookmarks.json");

    private string Theme => Path.Combine(_dir, "theme.json");

    [Fact]
    public void CreatesAllFilesWhenNoneExist()
    {
        DefaultConfigFiles.EnsureExist(Keybindings, Bookmarks, Theme);

        Assert.True(File.Exists(Keybindings));
        Assert.True(File.Exists(Bookmarks));
        Assert.True(File.Exists(Theme));
    }

    [Fact]
    public void CreatesTheConfigDirectoryIfItIsNotThereYet()
    {
        Assert.False(Directory.Exists(_dir));

        DefaultConfigFiles.EnsureExist(Keybindings, Bookmarks, Theme);

        Assert.True(Directory.Exists(_dir));
    }

    [Fact]
    public void KeybindingsStartAsAnEmptyObjectAndParseCleanly()
    {
        DefaultConfigFiles.EnsureExist(Keybindings, Bookmarks, Theme);

        KeymapLoad load = KeymapConfig.Load(Keybindings);

        Assert.Empty(load.Overrides);
        Assert.Empty(load.Problems);
    }

    [Fact]
    public void BookmarksStartAsAnEmptyListAndParseCleanly()
    {
        DefaultConfigFiles.EnsureExist(Keybindings, Bookmarks, Theme);

        BookmarkStore store = new(Bookmarks);

        Assert.Empty(store.Items);
    }

    [Fact]
    public void NeverOverwritesAFileThatIsAlreadyThere()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Keybindings, """{ "content.move_down": "j" }""");

        DefaultConfigFiles.EnsureExist(Keybindings, Bookmarks, Theme);

        Assert.Equal("""{ "content.move_down": "j" }""", File.ReadAllText(Keybindings));
    }

    [Fact]
    public void RunningItTwiceLeavesTheFilesAsTheyWere()
    {
        DefaultConfigFiles.EnsureExist(Keybindings, Bookmarks, Theme);
        string firstKeybindings = File.ReadAllText(Keybindings);
        string firstBookmarks = File.ReadAllText(Bookmarks);
        string firstTheme = File.ReadAllText(Theme);

        DefaultConfigFiles.EnsureExist(Keybindings, Bookmarks, Theme);

        Assert.Equal(firstKeybindings, File.ReadAllText(Keybindings));
        Assert.Equal(firstBookmarks, File.ReadAllText(Bookmarks));
        Assert.Equal(firstTheme, File.ReadAllText(Theme));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

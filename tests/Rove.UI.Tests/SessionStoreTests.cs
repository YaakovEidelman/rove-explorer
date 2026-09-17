using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class SessionStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(), "rove-session-" + Guid.NewGuid().ToString("N") + ".json");

    private SessionStore Store() => new(_file);

    [Fact]
    public void AMissingFileReadsAsNoSession()
    {
        Assert.Null(Store().Load());
    }

    [Fact]
    public void ASavedSessionComesBackOnTheNextLaunch()
    {
        SessionStore first = Store();
        first.Save(new([new("/one"), new("/two", "renamed")], 1));

        AppSession? loaded = Store().Load();

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Tabs.Length);
        Assert.Equal("/one", loaded.Tabs[0].Directory);
        Assert.Null(loaded.Tabs[0].Title);
        Assert.Equal("/two", loaded.Tabs[1].Directory);
        Assert.Equal("renamed", loaded.Tabs[1].Title);
        Assert.Equal(1, loaded.ActiveIndex);
    }

    [Fact]
    public void ARuinedFileReadsAsNoSessionRatherThanFailing()
    {
        File.WriteAllText(_file, "{ not json at all");

        Assert.Null(Store().Load());
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
        }
    }
}

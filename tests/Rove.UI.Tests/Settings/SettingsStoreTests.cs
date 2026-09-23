using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(), "rove-settings-" + Guid.NewGuid().ToString("N") + ".json");

    private SettingsStore Store() => new(_file);

    [Fact]
    public void AMissingFileReadsAsEveryDefault()
    {
        AppSettings settings = Store().Current;

        Assert.Equal("System", settings.Theme);
        Assert.False(settings.ShowHiddenByDefault);
        Assert.Equal("List", settings.DefaultView);
        Assert.True(settings.SortDownloadsByTime);
        Assert.True(settings.GroupByDate);
        Assert.False(settings.AutoUpdate);
    }

    [Fact]
    public void AChangeComesBackOnTheNextLaunch()
    {
        SettingsStore first = Store();
        first.Update(first.Current with { Theme = "Dark", ShowHiddenByDefault = true });

        SettingsStore second = Store();

        Assert.Equal("Dark", second.Current.Theme);
        Assert.True(second.Current.ShowHiddenByDefault);
    }

    [Fact]
    public void ARuinedFileReadsAsEveryDefaultRatherThanFailing()
    {
        File.WriteAllText(_file, "{ not json at all");

        AppSettings settings = Store().Current;

        Assert.Equal("System", settings.Theme);
    }

    [Fact]
    public void EveryUpdateIsAnnounced()
    {
        SettingsStore store = Store();
        int changes = 0;
        store.Changed += () => changes++;

        store.Update(store.Current with { ShowHiddenByDefault = true });

        Assert.Equal(1, changes);
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

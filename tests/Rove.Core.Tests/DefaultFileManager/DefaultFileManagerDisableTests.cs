using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class DefaultFileManagerDisableTests
{
    [Fact]
    public void DisablingAFreshClaimWithNothingBeforeRemovesTheKey()
    {
        using MimeAppsHome home = new();
        DefaultFileManager.Enable(home.MimeAppsPath, home.StatePath, "rove.desktop");

        bool disabled = DefaultFileManager.Disable(home.MimeAppsPath, home.StatePath);

        Assert.True(disabled);
        Assert.Null(DefaultFileManager.CurrentDefault(home.MimeAppsPath));
        Assert.False(File.Exists(home.StatePath));
    }

    [Fact]
    public void DisablingRestoresWhatWasThereBefore()
    {
        using MimeAppsHome home = new();
        Directory.CreateDirectory(Path.GetDirectoryName(home.MimeAppsPath)!);
        File.WriteAllText(home.MimeAppsPath, "[Default Applications]\ninode/directory=org.gnome.Nautilus.desktop\n");
        DefaultFileManager.Enable(home.MimeAppsPath, home.StatePath, "rove.desktop");

        DefaultFileManager.Disable(home.MimeAppsPath, home.StatePath);

        Assert.Equal("org.gnome.Nautilus.desktop", DefaultFileManager.CurrentDefault(home.MimeAppsPath));
    }

    [Fact]
    public void WithNothingToRevertItSaysSo()
    {
        using MimeAppsHome home = new();

        Assert.False(DefaultFileManager.Disable(home.MimeAppsPath, home.StatePath));
    }
}

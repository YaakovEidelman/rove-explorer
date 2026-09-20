using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class DefaultFileManagerStatusTests
{
    [Fact]
    public void NothingAtAllMeansNotInstalled()
    {
        using MimeAppsHome home = new();

        Assert.Equal(PortalStatus.NotInstalled, DefaultFileManager.CurrentStatus(home.MimeAppsPath, home.StatePath));
    }

    [Fact]
    public void AnExistingEntryMeansOwnedByOther()
    {
        using MimeAppsHome home = new();
        Directory.CreateDirectory(Path.GetDirectoryName(home.MimeAppsPath)!);
        File.WriteAllText(home.MimeAppsPath, "[Default Applications]\ninode/directory=org.gnome.Nautilus.desktop\n");

        Assert.Equal(PortalStatus.OwnedByOther, DefaultFileManager.CurrentStatus(home.MimeAppsPath, home.StatePath));
    }
}

using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public sealed class MimeAppsHome : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rove-mime-" + Guid.NewGuid().ToString("N"));

    public string MimeAppsPath => Path.Combine(Root, "config", "mimeapps.list");

    public string StatePath => Path.Combine(Root, "state", "mime-default.json");

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
        }
    }
}

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

public class DefaultFileManagerEnableTests
{
    [Fact]
    public void EnablingWithNoFileAtAllCreatesTheSection()
    {
        using MimeAppsHome home = new();

        bool enabled = DefaultFileManager.Enable(home.MimeAppsPath, home.StatePath, "rove.desktop");

        Assert.True(enabled);
        Assert.Equal(PortalStatus.OwnedByRove, DefaultFileManager.CurrentStatus(home.MimeAppsPath, home.StatePath));
        Assert.Equal("rove.desktop", DefaultFileManager.CurrentDefault(home.MimeAppsPath));
    }

    [Fact]
    public void EnablingOverSomeoneElsesEntryPreservesTheRestOfTheFile()
    {
        using MimeAppsHome home = new();
        Directory.CreateDirectory(Path.GetDirectoryName(home.MimeAppsPath)!);
        File.WriteAllText(home.MimeAppsPath,
            "[Default Applications]\n"
            + "text/plain=gedit.desktop\n"
            + "inode/directory=org.gnome.Nautilus.desktop\n"
            + "[Added Associations]\n"
            + "text/plain=gedit.desktop;\n");

        bool enabled = DefaultFileManager.Enable(home.MimeAppsPath, home.StatePath, "rove.desktop");

        Assert.True(enabled);
        string written = File.ReadAllText(home.MimeAppsPath);
        Assert.Contains("inode/directory=rove.desktop", written, StringComparison.Ordinal);
        Assert.Contains("text/plain=gedit.desktop\n", written, StringComparison.Ordinal);
        Assert.Contains("[Added Associations]", written, StringComparison.Ordinal);
    }

    [Fact]
    public void EnablingWhatsAlreadyOursDoesNothingFurther()
    {
        using MimeAppsHome home = new();
        DefaultFileManager.Enable(home.MimeAppsPath, home.StatePath, "rove.desktop");

        bool enabledAgain = DefaultFileManager.Enable(home.MimeAppsPath, home.StatePath, "rove.desktop");

        Assert.False(enabledAgain);
    }
}

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

using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

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
        string[] written = File.ReadAllLines(home.MimeAppsPath);
        Assert.Contains("inode/directory=rove.desktop", written);
        Assert.Contains("text/plain=gedit.desktop", written);
        Assert.Contains("[Added Associations]", written);
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

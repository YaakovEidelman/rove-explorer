using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class PayloadTests
{
    [Fact]
    public void EveryFileOfABuildIsCopiedExceptTheSymbols()
    {
        using InstallHome home = new();
        string source = Path.GetDirectoryName(home.Build("download"))!;

        Assert.True(Payload.Install(source, home.LibDir, "Rove"));

        Assert.Equal("a binary", File.ReadAllText(Path.Combine(home.LibDir, "Rove")));
        Assert.Equal("skia a binary", File.ReadAllText(Path.Combine(home.LibDir, "libSkiaSharp.so")));
        Assert.True(File.Exists(Path.Combine(home.LibDir, "libHarfBuzzSharp.so")));
        Assert.False(File.Exists(Path.Combine(home.LibDir, "Rove.pdb")));
    }

    [Fact]
    public void AnUpdateReplacesEveryFileAndLeavesNoLitter()
    {
        using InstallHome home = new();
        Payload.Install(Path.GetDirectoryName(home.Build("old", "version one"))!, home.LibDir, "Rove");

        Assert.True(Payload.Install(Path.GetDirectoryName(home.Build("new", "version two"))!, home.LibDir, "Rove"));

        Assert.Equal("version two", File.ReadAllText(Path.Combine(home.LibDir, "Rove")));
        Assert.Equal("skia version two", File.ReadAllText(Path.Combine(home.LibDir, "libSkiaSharp.so")));
        Assert.Empty(Directory.EnumerateFiles(home.LibDir, "*.new"));
        Assert.Empty(Directory.EnumerateFiles(home.LibDir, "*.old"));
    }

    [Fact]
    public void RemovingTakesTheWholeFolder()
    {
        using InstallHome home = new();
        Payload.Install(Path.GetDirectoryName(home.Build("download"))!, home.LibDir, "Rove");

        Payload.Remove(home.LibDir);

        Assert.False(Directory.Exists(home.LibDir));
    }

    [Fact]
    public void NothingBesideTheProgramAndItsLibrariesIsTakenAlong()
    {
        using InstallHome home = new();
        string source = Path.GetDirectoryName(home.Build("download"))!;
        File.WriteAllText(Path.Combine(source, "tax-return.pdf"), "not ours");
        File.WriteAllText(Path.Combine(source, "someone-elses.exe"), "not ours either");
        File.WriteAllText(Path.Combine(source, "libVersioned.so.1.2"), "a versioned library");

        Assert.True(Payload.Install(source, home.LibDir, "Rove"));

        Assert.True(File.Exists(Path.Combine(home.LibDir, "Rove")));
        Assert.True(File.Exists(Path.Combine(home.LibDir, "libSkiaSharp.so")));
        Assert.True(File.Exists(Path.Combine(home.LibDir, "libVersioned.so.1.2")));
        Assert.False(File.Exists(Path.Combine(home.LibDir, "tax-return.pdf")));
        Assert.False(File.Exists(Path.Combine(home.LibDir, "someone-elses.exe")));
        Assert.False(File.Exists(Path.Combine(home.LibDir, "Rove.pdb")));
    }

    [Fact]
    public void AFolderWithNoProgramInItInstallsNothing()
    {
        using InstallHome home = new();
        string source = home.Sub("junk");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "libSkiaSharp.so"), "orphan");

        Assert.False(Payload.Install(source, home.LibDir, "Rove"));
    }

    [Fact]
    public void AnEmptySourceInstallsNothing()
    {
        using InstallHome home = new();
        string empty = home.Sub("empty");
        Directory.CreateDirectory(empty);

        Assert.False(Payload.Install(empty, home.LibDir, "Rove"));
    }
}

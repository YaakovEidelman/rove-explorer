using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class StartLocationTests
{
    /// <summary>
    /// The same imaginary place, spelled the way the running system spells an
    /// absolute path. Which argument wins is the same rule everywhere; only
    /// the shape of a root differs, and a test that writes one root by hand
    /// is a test of one system.
    /// </summary>
    private static string Abs(string path) =>
        OperatingSystem.IsWindows() ? "C:" + path.Replace('/', Path.DirectorySeparatorChar) : path;

    private static readonly string Folder = Abs("/home/me/projects");
    private static readonly string File_ = Abs("/home/me/projects/notes.txt");

    private static Func<string, bool> Only(string path) =>
        candidate => string.Equals(candidate, path, StringComparison.Ordinal);

    private static Func<string, bool> Nothing => _ => false;

    [Fact]
    public void AFolderOnTheCommandLineIsWhereRoveOpens()
    {
        Assert.Equal(Folder, StartLocation.From([Folder], Only(Folder), Nothing));
    }

    [Fact]
    public void AFileOnTheCommandLineOpensTheFolderItSitsIn()
    {
        Assert.Equal(Folder, StartLocation.From([File_], Nothing, Only(File_)));
    }

    [Fact]
    public void ALauncherThatPassesAUriIsUnderstoodTheSameWay()
    {
        string uri = new Uri(Folder).AbsoluteUri;

        Assert.StartsWith("file:///", uri);
        Assert.Equal(Folder, StartLocation.From([uri], Only(Folder), Nothing));
    }

    [Fact]
    public void SwitchesAreNotMistakenForPaths()
    {
        Assert.Equal(Folder, StartLocation.From(["--install", "-v", Folder], Only(Folder), Nothing));
    }

    [Fact]
    public void TheFirstArgumentThatExistsWins()
    {
        Assert.Equal(Folder, StartLocation.From([Abs("/gone"), Folder], Only(Folder), Nothing));
    }

    [Fact]
    public void NoArgumentsAtAllMeansTheUsualStartingPlace()
    {
        Assert.Equal(PathCompare.DefaultStartDirectory(), StartLocation.From([], Nothing, Nothing));
    }

    [Fact]
    public void ALaunchWithNoArgumentListMeansTheUsualStartingPlace()
    {
        Assert.Equal(PathCompare.DefaultStartDirectory(), StartLocation.From(null, Nothing, Nothing));
    }

    [Fact]
    public void APathThatIsNeitherFileNorFolderIsIgnored()
    {
        Assert.Equal(
            PathCompare.DefaultStartDirectory(),
            StartLocation.From([Abs("/does/not/exist")], Nothing, Nothing));
    }

    [Fact]
    public void ExplicitTargetIsTheFolderWhenOneIsGiven()
    {
        Assert.Equal(Folder, StartLocation.ExplicitTarget([Folder], Only(Folder), Nothing));
    }

    [Fact]
    public void ExplicitTargetIsNullWhenNothingResolves()
    {
        Assert.Null(StartLocation.ExplicitTarget([], Nothing, Nothing));
        Assert.Null(StartLocation.ExplicitTarget([Abs("/does/not/exist")], Nothing, Nothing));
    }
}

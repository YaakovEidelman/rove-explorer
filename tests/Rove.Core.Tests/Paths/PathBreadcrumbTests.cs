using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PathBreadcrumbTests
{
    private static readonly char Sep = System.IO.Path.DirectorySeparatorChar;

    private static string[] Labels(string path) =>
        [.. PathBreadcrumb.Of(path).Select(crumb => crumb.Label)];

    private static string[] Paths(string path) =>
        [.. PathBreadcrumb.Of(path).Select(crumb => crumb.FullPath)];

    [Fact]
    public void EveryStepOfThePathGetsItsOwnCrumb()
    {
        string root = PathCompare.OSRootPath();

        Assert.Equal(
            [root, "one", "two", "three"],
            Labels(root + string.Join(Sep, "one", "two", "three")));
    }

    [Fact]
    public void ACrumbStandsForEverythingUpToAndIncludingItself()
    {
        string root = PathCompare.OSRootPath();
        string path = root + string.Join(Sep, "one", "two");

        Assert.Equal(
            [root, System.IO.Path.Combine(root, "one"), path],
            Paths(path));
    }

    [Fact]
    public void TheRootOnItsOwnIsASingleCrumb()
    {
        string root = PathCompare.OSRootPath();

        Assert.Equal([root], Labels(root));
    }

    [Fact]
    public void ATrailingSeparatorAddsNoEmptyCrumb()
    {
        string root = PathCompare.OSRootPath();

        Assert.Equal(Labels(root + "one"), Labels(root + "one" + Sep));
    }

    [Fact]
    public void NoPathMeansNoCrumbs()
    {
        Assert.Empty(PathBreadcrumb.Of(string.Empty));
        Assert.Empty(PathBreadcrumb.Of("   "));
    }

    [Fact]
    public void ALongPathIsBrokenUpTheSameWayAsAShortOne()
    {
        using TempDir temp = new();
        string deep = temp.DeepDir();

        PathCrumb[] crumbs = PathBreadcrumb.Of(deep);

        Assert.True(crumbs.Length > 1);
        Assert.Equal(deep, crumbs[^1].FullPath);
        Assert.DoesNotContain(crumbs, crumb => crumb.Label.Contains("?"));
    }

    [Fact]
    public void CollapseFoldsTheRootIntoOneLabeledCrumb()
    {
        using TempDir tmp = new();
        string root = tmp.Dir("Trash");
        string nested = tmp.Dir(System.IO.Path.Combine("Trash", "OldProject", "src"));

        PathCrumb[] crumbs = PathBreadcrumb.Of(nested, (root, "Trash"));

        Assert.Equal(["Trash", "OldProject", "src"], [.. crumbs.Select(c => c.Label)]);
        Assert.Equal(root, crumbs[0].FullPath);
        Assert.True(crumbs[^1].IsLast);
    }

    [Fact]
    public void CollapseAtExactlyTheRootIsASingleCrumb()
    {
        using TempDir tmp = new();
        string root = tmp.Dir("Trash");

        PathCrumb[] crumbs = PathBreadcrumb.Of(root, (root, "Trash"));

        Assert.Equal(["Trash"], [.. crumbs.Select(c => c.Label)]);
        Assert.True(crumbs[0].IsLast);
    }

    [Fact]
    public void CollapseDoesNotMatchASimilarlyNamedSibling()
    {
        using TempDir tmp = new();
        string root = tmp.Dir("Trash");
        string sibling = tmp.Dir("TrashCan");

        PathCrumb[] crumbs = PathBreadcrumb.Of(sibling, (root, "Trash"));

        Assert.Equal(Labels(sibling), crumbs.Select(c => c.Label).ToArray());
        Assert.DoesNotContain(crumbs, c => c.Label == "Trash");
    }

    [Fact]
    public void CollapseIgnoresAPathThatIsNotUnderTheRoot()
    {
        using TempDir tmp = new();
        string root = tmp.Dir("Trash");
        string elsewhere = tmp.Dir("Documents");

        PathCrumb[] crumbs = PathBreadcrumb.Of(elsewhere, (root, "Trash"));

        Assert.Equal(Labels(elsewhere), crumbs.Select(c => c.Label).ToArray());
    }
}

using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PathGuardTests
{
    [Theory]
    [InlineData("notes.txt")]
    [InlineData("My Folder")]
    [InlineData(".gitignore")]
    public void ValidNames_PassValidation(string name)
    {
        Assert.Null(PathGuard.Validate(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData(@"..\escape")]
    [InlineData("c:evil")]
    public void InvalidNames_FailValidation(string name)
    {
        Assert.NotNull(PathGuard.Validate(name));
    }

    [Fact]
    public void WindowsReservedAndTrailingDot_FailOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;
        Assert.NotNull(PathGuard.Validate("CON"));
        Assert.NotNull(PathGuard.Validate("con.txt"));
        Assert.NotNull(PathGuard.Validate("name."));
        Assert.NotNull(PathGuard.Validate("name "));
    }

    [Fact]
    public void SafeCombine_RejectsTraversal()
    {
        using TempDir tmp = new();
        string? combined = PathGuard.SafeCombine(tmp.Path, @"..\escape", out string? reason);
        Assert.Null(combined);
        Assert.NotNull(reason);
    }

    [Fact]
    public void SafeCombine_ProducesPathInsideDirectory()
    {
        using TempDir tmp = new();
        string? combined = PathGuard.SafeCombine(tmp.Path, "child.txt", out string? reason);
        Assert.Null(reason);
        Assert.NotNull(combined);
        Assert.StartsWith(tmp.Path, combined!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsSameOrDescendant_DetectsNesting()
    {
        using TempDir tmp = new();
        string sub = tmp.Dir("a");
        string deeper = tmp.Dir(@"a\b");
        Assert.True(PathGuard.IsSameOrDescendant(sub, deeper));
        Assert.True(PathGuard.IsSameOrDescendant(sub, sub));
        Assert.False(PathGuard.IsSameOrDescendant(deeper, sub));
    }

    [Fact]
    public void IsSameOrDescendant_DoesNotMatchASimilarlyNamedSibling()
    {
        using TempDir tmp = new();
        string trash = tmp.Dir("Trash");
        string trashCan = tmp.Dir("TrashCan");
        Assert.False(PathGuard.IsSameOrDescendant(trash, trashCan));
        Assert.False(PathGuard.IsSameOrDescendant(trashCan, trash));
    }

    [Fact]
    public void IsSameOrDescendant_ResolvesTraversalBeforeComparing()
    {
        using TempDir tmp = new();
        string trash = tmp.Dir("Trash");
        string sibling = tmp.Dir("Elsewhere");
        string viaTraversal = System.IO.Path.Combine(sibling, "..", "Trash", "item");
        Assert.True(PathGuard.IsSameOrDescendant(trash, viaTraversal));
        Assert.False(PathGuard.IsSameOrDescendant(sibling, viaTraversal));
    }
}

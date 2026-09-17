using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PathResolverTests
{
    [Fact]
    public void AnAbsolutePathComesBackAsItself()
    {
        using TempDir tmp = new();

        string? resolved = PathResolver.Resolve(tmp.Path, Directory.GetCurrentDirectory());

        Assert.True(PathCompare.PathMatches(tmp.Path, resolved!));
    }

    [Fact]
    public void ARelativePathIsMeasuredFromTheFolderYouAreIn()
    {
        using TempDir tmp = new();
        tmp.Dir("sub");

        string? resolved = PathResolver.Resolve("sub", tmp.Path);

        Assert.True(PathCompare.PathMatches(tmp.Sub("sub"), resolved!));
    }

    [Fact]
    public void DotDotWalksUp()
    {
        using TempDir tmp = new();
        string sub = tmp.Dir("sub");

        string? resolved = PathResolver.Resolve("..", sub);

        Assert.True(PathCompare.PathMatches(tmp.Path, resolved!));
    }

    [Fact]
    public void ATrailingSeparatorIsDropped()
    {
        using TempDir tmp = new();

        string? resolved = PathResolver.Resolve(tmp.Path + Path.DirectorySeparatorChar, tmp.Path);

        Assert.Equal(Path.TrimEndingDirectorySeparator(tmp.Path), resolved);
    }

    [Fact]
    public void QuotesAroundAPastedPathComeOff()
    {
        using TempDir tmp = new();

        string? resolved = PathResolver.Resolve($"\"{tmp.Path}\"", Directory.GetCurrentDirectory());

        Assert.True(PathCompare.PathMatches(tmp.Path, resolved!));
    }

    [Fact]
    public void TildeIsTheHomeDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.True(PathCompare.PathMatches(home, PathResolver.Resolve("~", home)!));
        Assert.True(PathCompare.PathMatches(
            Path.Combine(home, "Documents"),
            PathResolver.Resolve("~/Documents", home)!));
    }

    [Fact]
    public void ATildeInTheMiddleIsJustACharacter()
    {
        using TempDir tmp = new();

        string? resolved = PathResolver.Resolve("back~up", tmp.Path);

        Assert.True(PathCompare.PathMatches(tmp.Sub("back~up"), resolved!));
    }

    [Fact]
    public void AnEnvironmentVariableIsExpanded()
    {
        using TempDir tmp = new();
        Environment.SetEnvironmentVariable("ROVE_TEST_DIR", tmp.Path);
        try
        {
            string written = OperatingSystem.IsWindows() ? "%ROVE_TEST_DIR%" : "$ROVE_TEST_DIR";

            string? resolved = PathResolver.Resolve(written, Directory.GetCurrentDirectory());

            Assert.True(PathCompare.PathMatches(tmp.Path, resolved!));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROVE_TEST_DIR", null);
        }
    }

    [Fact]
    public void NothingTypedResolvesToNothing()
    {
        Assert.Null(PathResolver.Resolve("", Directory.GetCurrentDirectory()));
        Assert.Null(PathResolver.Resolve("   ", Directory.GetCurrentDirectory()));
    }
}

public class ResolvePathEndpointTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void AFolderComesBackAsADirectory()
    {
        using TempDir tmp = new();
        tmp.Dir("sub");

        CommandResult<FolderItem?> result = _actions.ResolvePath(new("sub", tmp.Path));

        Assert.True(result.IsOk);
        Assert.True(result.Data!.IsDirectory);
        Assert.True(PathCompare.PathMatches(tmp.Sub("sub"), result.Data.FullPath));
    }

    [Fact]
    public void AFileComesBackAsAFile()
    {
        using TempDir tmp = new();
        tmp.File("notes.txt", "hello");

        CommandResult<FolderItem?> result = _actions.ResolvePath(new("notes.txt", tmp.Path));

        Assert.True(result.IsOk);
        Assert.False(result.Data!.IsDirectory);
    }

    [Fact]
    public void APathWithNothingAtItFails()
    {
        using TempDir tmp = new();

        CommandResult<FolderItem?> result = _actions.ResolvePath(new("nowhere", tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }

    [Fact]
    public void TextThatIsNoPathAtAllFails()
    {
        using TempDir tmp = new();

        CommandResult<FolderItem?> result = _actions.ResolvePath(new("   ", tmp.Path));

        Assert.False(result.IsOk);
        Assert.Equal("invalid_path", result.Reason);
    }

    [Fact]
    public void ALongPathStillResolves()
    {
        using TempDir tmp = new();
        string deep = tmp.DeepDir();

        CommandResult<FolderItem?> result = _actions.ResolvePath(new(deep, tmp.Path));

        Assert.True(result.IsOk);
        Assert.True(result.Data!.IsDirectory);
    }
}

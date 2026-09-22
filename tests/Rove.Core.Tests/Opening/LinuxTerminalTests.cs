using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class LinuxTerminalTests
{
    private const string UsrBin = "/usr/bin";

    private static Func<string, bool> Present(params string[] programs) =>
        candidate => programs.Contains(candidate, StringComparer.Ordinal);

    [Fact]
    public void TheTerminalVariableWinsOverEverythingElse()
    {
        string? found = LinuxTerminal.Find(UsrBin, "foot", Present("/usr/bin/foot", "/usr/bin/xterm"));

        Assert.Equal("/usr/bin/foot", found);
    }

    [Fact]
    public void AFullPathInTheTerminalVariableIsUsedAsIs()
    {
        string? found = LinuxTerminal.Find(UsrBin, "/opt/term/best", Present("/opt/term/best", "/usr/bin/xterm"));

        Assert.Equal("/opt/term/best", found);
    }

    [Fact]
    public void ATerminalVariableThatIsNotInstalledFallsBackToTheKnownList()
    {
        string? found = LinuxTerminal.Find(UsrBin, "gone", Present("/usr/bin/konsole"));

        Assert.Equal("/usr/bin/konsole", found);
    }

    [Fact]
    public void TheDesktopsOwnLauncherComesBeforeNamedTerminals()
    {
        string? found = LinuxTerminal.Find(UsrBin, null, Present("/usr/bin/kitty", "/usr/bin/xdg-terminal-exec"));

        Assert.Equal("/usr/bin/xdg-terminal-exec", found);
    }

    [Fact]
    public void NoTerminalAnywhereFindsNothing()
    {
        Assert.Null(LinuxTerminal.Find(UsrBin, null, Present()));
    }
}

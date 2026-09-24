using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class LinuxTerminalTests
{
    private const string UsrBin = "/usr/bin";

    private static string In(string directory, string program) =>
        System.IO.Path.Combine(directory, program);

    private static Func<string, bool> Present(params string[] programs) =>
        candidate => programs.Contains(candidate, StringComparer.Ordinal);

    [Fact]
    public void TheTerminalVariableWinsOverEverythingElse()
    {
        string? found = LinuxTerminal.Find(UsrBin, "foot", Present(In(UsrBin, "foot"), In(UsrBin, "xterm")));

        Assert.Equal(In(UsrBin, "foot"), found);
    }

    [Fact]
    public void AFullPathInTheTerminalVariableIsUsedAsIs()
    {
        string? found = LinuxTerminal.Find(UsrBin, "/opt/term/best", Present("/opt/term/best", In(UsrBin, "xterm")));

        Assert.Equal("/opt/term/best", found);
    }

    [Fact]
    public void ATerminalVariableThatIsNotInstalledFallsBackToTheKnownList()
    {
        string? found = LinuxTerminal.Find(UsrBin, "gone", Present(In(UsrBin, "konsole")));

        Assert.Equal(In(UsrBin, "konsole"), found);
    }

    [Fact]
    public void TheDesktopsOwnLauncherComesBeforeNamedTerminals()
    {
        string? found = LinuxTerminal.Find(UsrBin, null, Present(In(UsrBin, "kitty"), In(UsrBin, "xdg-terminal-exec")));

        Assert.Equal(In(UsrBin, "xdg-terminal-exec"), found);
    }

    [Fact]
    public void NoTerminalAnywhereFindsNothing()
    {
        Assert.Null(LinuxTerminal.Find(UsrBin, null, Present()));
    }
}

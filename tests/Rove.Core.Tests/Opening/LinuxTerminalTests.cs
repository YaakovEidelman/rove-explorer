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

    [Theory]
    [InlineData("/usr/bin/xdg-terminal-exec", "")]
    [InlineData("/usr/bin/kitty", "")]
    [InlineData("/usr/bin/gnome-terminal", "--")]
    [InlineData("/usr/bin/wezterm", "start --")]
    [InlineData("/usr/bin/xfce4-terminal", "-x")]
    [InlineData("/usr/bin/alacritty", "-e")]
    public void ACommandRunsThroughAShellThatWaitsBeforeClosing(string terminal, string prefix)
    {
        string[] arguments = LinuxTerminal.RunArguments(terminal, ["sudo", "pacman", "-S", "udisks2"]);

        string[] expected = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected, arguments[..expected.Length]);
        Assert.Equal(["sh", "-c"], arguments[expected.Length..(expected.Length + 2)]);
        Assert.Contains("read", arguments[expected.Length + 2]);
        Assert.Equal(["sh", "sudo", "pacman", "-S", "udisks2"], arguments[(expected.Length + 3)..]);
    }
}

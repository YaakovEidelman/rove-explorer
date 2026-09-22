namespace Rove.Core.Services;

public static class TerminalLauncher
{
    public static string? Start(string directory)
    {
        string real = LongPath.Display(directory);
        if (!Directory.Exists(real))
            return $"{Path.GetFileName(real)} is not a folder that exists any more.";

        return OperatingSystem.IsWindows()
            ? WindowsTerminal.Start(real)
            : LinuxTerminal.Start(real);
    }
}

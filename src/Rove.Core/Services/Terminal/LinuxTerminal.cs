using System.ComponentModel;
using System.Diagnostics;

namespace Rove.Core.Services;

public static class LinuxTerminal
{
    private static readonly string[] _known =
    [
        "xdg-terminal-exec",
        "x-terminal-emulator",
        "ghostty",
        "kitty",
        "alacritty",
        "foot",
        "wezterm",
        "gnome-terminal",
        "konsole",
        "xfce4-terminal",
        "tilix",
        "xterm",
    ];

    public static string? Start(string directory)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        string? terminal = Find(path, Environment.GetEnvironmentVariable("TERMINAL"), FileOpener.IsRunnable);
        if (terminal is null)
            return "No terminal program was found. Set the TERMINAL environment variable to the one you use.";

        try
        {
            using Process? started = Process.Start(new ProcessStartInfo(terminal)
            {
                UseShellExecute = false,
                WorkingDirectory = directory,
            });
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return ex.Message;
        }
    }

    internal static string? Find(string? pathVariable, string? preferred, Func<string, bool> exists)
    {
        if (preferred is { Length: > 0 })
        {
            string name = preferred.Trim();
            if (Path.IsPathRooted(name))
            {
                if (exists(name))
                    return name;
            }
            else if (FileOpener.FindProgram(pathVariable, name, exists) is { } chosen)
            {
                return chosen;
            }
        }

        foreach (string program in _known)
        {
            if (FileOpener.FindProgram(pathVariable, program, exists) is { } found)
                return found;
        }
        return null;
    }
}

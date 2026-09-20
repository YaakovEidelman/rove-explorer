using System.ComponentModel;
using System.Diagnostics;

namespace Rove.UI.Services;

internal static class SelfDelete
{
    private static readonly TimeSpan _grace = TimeSpan.FromSeconds(2);

    public static void After(string folder)
    {
        if (!IsSafeToRemove(folder))
            return;

        try
        {
            int seconds = (int)_grace.TotalSeconds;
            ProcessStartInfo start = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("cmd.exe",
                    $"/c ping -n {seconds + 2} 127.0.0.1 > nul & rmdir /s /q \"{folder}\"")
                : new ProcessStartInfo("/bin/sh",
                    $"-c \"sleep {seconds}; rm -rf {ShellQuote(folder)}\"");

            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WorkingDirectory = Path.GetTempPath();
            Process.Start(start);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
        }
    }

    private static bool IsSafeToRemove(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        try
        {
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            return Path.GetDirectoryName(full) is { Length: > 0 } && Path.GetFileName(full).Length > 0;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string ShellQuote(string path) => "'" + path.Replace("'", "'\\''") + "'";
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Rove.UI.Services;

/// <summary>
/// Removes an installed build once the program using it has gone. A program
/// cannot delete the folder it is running from — Windows will not delete a
/// file that is open, and the libraries beside it are open too — so the last
/// step is handed to a small command that waits for this process to end and
/// then does it.
/// </summary>
internal static class SelfDelete
{
    private static readonly TimeSpan _grace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Removes <paramref name="folder"/> and everything in it, shortly. It
    /// has to be a named folder with a parent — never a drive, and never a
    /// home directory.
    /// </summary>
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
            // Nothing left to try; the folder stays and can be removed by hand.
        }
    }

    /// <summary>A folder with no parent is a drive or a root, and is nobody's to remove.</summary>
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

    /// <summary>Single quotes around it, and any single quote inside broken out.</summary>
    private static string ShellQuote(string path) => "'" + path.Replace("'", "'\\''") + "'";
}

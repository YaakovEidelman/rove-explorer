using System.ComponentModel;
using System.Diagnostics;

namespace Rove.Core.Services;

public static class WindowsTerminal
{
    public static string? Start(string directory)
    {
        ProcessStartInfo modern = new("wt.exe") { UseShellExecute = false };
        modern.ArgumentList.Add("-d");
        modern.ArgumentList.Add(directory);

        ProcessStartInfo classic = new("cmd.exe") { UseShellExecute = true, WorkingDirectory = directory };

        try
        {
            using Process? started = Process.Start(modern);
            return null;
        }
        catch (Win32Exception)
        {
        }

        try
        {
            using Process? started = Process.Start(classic);
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return ex.Message;
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;

namespace Rove.Core.Services;

public static class SessionBus
{
    public static void ReloadConfig()
    {
        try
        {
            ProcessStartInfo info = new("dbus-send")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            info.ArgumentList.Add("--session");
            info.ArgumentList.Add("--dest=org.freedesktop.DBus");
            info.ArgumentList.Add("--type=method_call");
            info.ArgumentList.Add("--print-reply");
            info.ArgumentList.Add("/org/freedesktop/DBus");
            info.ArgumentList.Add("org.freedesktop.DBus.ReloadConfig");

            using Process? process = Process.Start(info);
            if (process is null)
                return;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(2000))
                process.Kill();
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException)
        {
        }
    }

    public static void KillOwner(string busName)
    {
        if (FindProcessId(busName) is not { } pid)
            return;
        try
        {
            Process.GetProcessById(pid).Kill();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
        }
    }

    public static int? FindProcessId(string busName)
    {
        try
        {
            ProcessStartInfo info = new("dbus-send")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            info.ArgumentList.Add("--session");
            info.ArgumentList.Add("--dest=org.freedesktop.DBus");
            info.ArgumentList.Add("--type=method_call");
            info.ArgumentList.Add("--print-reply=literal");
            info.ArgumentList.Add("/org/freedesktop/DBus");
            info.ArgumentList.Add("org.freedesktop.DBus.GetConnectionUnixProcessID");
            info.ArgumentList.Add($"string:{busName}");

            using Process? process = Process.Start(info);
            if (process is null)
                return null;
            string output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(2000))
            {
                process.Kill();
                return null;
            }
            if (process.ExitCode != 0)
                return null;

            string[] tokens = output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                if (tokens[i] == "uint32" && int.TryParse(tokens[i + 1], out int pid))
                    return pid;
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}

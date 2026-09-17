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
}

using System.ComponentModel;
using System.Diagnostics;

namespace Rove.Core.Services;

public static class WindowsOpenWith
{
    public static string? Start(string path)
    {
        ProcessStartInfo info = new("rundll32.exe") { UseShellExecute = false };
        info.ArgumentList.Add("shell32.dll,OpenAs_RunDLL");
        info.ArgumentList.Add(LongPath.Display(path));

        try
        {
            using Process? started = Process.Start(info);
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return ex.Message;
        }
    }
}

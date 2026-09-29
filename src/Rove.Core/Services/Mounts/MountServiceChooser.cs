namespace Rove.Core.Services;

public static class MountServiceChooser
{
    public static IMountService? CreateForHost()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsMounts();
        if (!OperatingSystem.IsLinux())
            return null;
        IMountService? gio = FileOpener.FindProgram(Environment.GetEnvironmentVariable("PATH"), "gio", FileOpener.IsRunnable)
            is { } program
            ? new GioMounts(program)
            : null;
        return new LinuxMounts(new UDisksMounts(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), gio);
    }
}

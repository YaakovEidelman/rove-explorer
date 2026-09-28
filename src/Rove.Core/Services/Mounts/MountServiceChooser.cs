namespace Rove.Core.Services;

public static class MountServiceChooser
{
    public static IMountService? CreateForHost()
    {
        if (!OperatingSystem.IsLinux())
            return null;
        return FileOpener.FindProgram(Environment.GetEnvironmentVariable("PATH"), "gio", FileOpener.IsRunnable)
            is { } gio
            ? new GioMounts(gio)
            : null;
    }
}

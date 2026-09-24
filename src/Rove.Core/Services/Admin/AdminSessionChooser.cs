namespace Rove.Core.Services;

public static class AdminSessionChooser
{
    public static IAdminSession? CreateForHost() =>
        OperatingSystem.IsLinux() ? LinuxAdminSession.ForPkexec() : null;
}

using System.Runtime.Versioning;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public static class FileManagerBusInstall
{
    public static string ServiceFilePath(string dataHome) =>
        Path.Combine(dataHome, "dbus-1", "services", FileManagerBusFiles.ServiceFileName);

    public static void Install(string dataHome, string executable)
    {
        bool changed = AtomicFileWrite.WriteIfDifferent(
            ServiceFilePath(dataHome), FileManagerBusFiles.ServiceFileContents(executable));
        if (changed)
            SessionBus.ReloadConfig();
    }

    public static void Withdraw(string dataHome)
    {
        if (AtomicFileWrite.TryDelete(ServiceFilePath(dataHome)))
            SessionBus.ReloadConfig();
    }
}

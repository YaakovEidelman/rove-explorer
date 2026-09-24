using System.Diagnostics;
using System.Runtime.Versioning;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public static class PortalInstall
{
    private const string PreferredSection = "[preferred]";

    public static string PortalFilePath(string dataHome) =>
        Path.Combine(dataHome, "xdg-desktop-portal", "portals", PortalFiles.PortalFileName);

    public static string ServiceFilePath(string dataHome) =>
        Path.Combine(dataHome, "dbus-1", "services", PortalFiles.ServiceFileName);

    public static string ConfigPath(string configHome, string? currentDesktop = null) =>
        Path.Combine(configHome, "xdg-desktop-portal", ConfigFileName(currentDesktop));

    private static string ConfigFileName(string? currentDesktop)
    {
        if (string.IsNullOrEmpty(currentDesktop))
            return "portals.conf";

        int colon = currentDesktop.IndexOf(':');
        string desktop = colon < 0 ? currentDesktop : currentDesktop[..colon];
        return desktop.Length == 0 ? "portals.conf" : $"{desktop.ToLowerInvariant()}-portals.conf";
    }

    public static void Advertise(string dataHome, string executable)
    {
        bool changed = AtomicFileWrite.WriteIfDifferent(PortalFilePath(dataHome), PortalFiles.PortalFileContents());
        changed |= AtomicFileWrite.WriteIfDifferent(ServiceFilePath(dataHome), PortalFiles.ServiceFileContents(executable));
        if (changed)
            SessionBus.ReloadConfig();
    }

    public static void Withdraw(string dataHome, string portalExecutable)
    {
        KillRunning(portalExecutable);
        bool changed = AtomicFileWrite.TryDelete(PortalFilePath(dataHome));
        changed |= AtomicFileWrite.TryDelete(ServiceFilePath(dataHome));
        if (changed)
            SessionBus.ReloadConfig();
    }

    public static PortalStatus CurrentStatus(string configPath, string statePath) =>
        PortalInstallState.Read(statePath) is not null ? PortalStatus.OwnedByRove
        : File.Exists(configPath) ? PortalStatus.OwnedByOther
        : PortalStatus.NotInstalled;

    public static bool HasAskedAboutDefault(string askedPath) => File.Exists(askedPath);

    public static void MarkAskedAboutDefault(string askedPath)
    {
        try
        {
            if (Path.GetDirectoryName(askedPath) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(askedPath, "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static bool Enable(
        string dataHome, string configPath, string statePath, string executable, string preferredName,
        Action? restartDesktopPortal = null)
    {
        Advertise(dataHome, executable);
        bool claimed = CurrentStatus(configPath, statePath) != PortalStatus.OwnedByRove
            && ClaimNow(configPath, statePath, preferredName);
        if (claimed)
            restartDesktopPortal?.Invoke();
        return claimed;
    }

    public static bool Disable(
        string dataHome, string configPath, string statePath, string portalExecutable,
        Action? restartDesktopPortal = null)
    {
        bool reverted = RevertBackend(configPath, statePath);
        Withdraw(dataHome, portalExecutable);
        if (reverted)
            restartDesktopPortal?.Invoke();
        return reverted;
    }

    public static bool RevertBackend(string configPath, string statePath)
    {
        PortalInstallState? state = PortalInstallState.Read(statePath);
        if (state is null)
            return false;

        string target = state.ConfigPath is { Length: > 0 } claimed ? claimed : configPath;

        try
        {
            if (state.PriorContent is null)
                File.Delete(target);
            else
            {
                if (Path.GetDirectoryName(target) is { Length: > 0 } parent)
                    Directory.CreateDirectory(parent);
                File.WriteAllText(target, state.PriorContent);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        PortalInstallState.Delete(statePath);
        return true;
    }

    private static bool ClaimNow(string configPath, string statePath, string preferredName)
    {
        string? prior = ReadIfPresent(configPath);
        return PortalInstallState.For(prior, configPath).Write(statePath) && WriteConfig(configPath, preferredName);
    }

    private static bool WriteConfig(string configPath, string preferredName)
    {
        try
        {
            if (Path.GetDirectoryName(configPath) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(configPath,
                PreferredSection + "\n" + $"org.freedesktop.impl.portal.FileChooser={preferredName}\n");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? ReadIfPresent(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void KillRunning(string portalExecutable)
    {
        try
        {
            foreach (string procDir in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(procDir), out int pid))
                    continue;

                string? exe;
                try
                {
                    exe = new FileInfo(Path.Combine(procDir, "exe")).LinkTarget;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (exe != portalExecutable && exe != portalExecutable + " (deleted)")
                    continue;

                try
                {
                    Process.GetProcessById(pid).Kill();
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void RestartDesktopPortal() => SessionBus.KillOwner("org.freedesktop.portal.Desktop");
}

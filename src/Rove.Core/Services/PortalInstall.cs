using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Rove.Core.Services;

public enum PortalStatus
{
    NotInstalled,
    OwnedByRove,
    OwnedByOther,
}

[SupportedOSPlatform("linux")]
public static class PortalInstall
{
    private const string PreferredSection = "[preferred]";

    public static string PortalFilePath(string dataHome) =>
        Path.Combine(dataHome, "xdg-desktop-portal", "portals", PortalFiles.PortalFileName);

    public static string ServiceFilePath(string dataHome) =>
        Path.Combine(dataHome, "dbus-1", "services", PortalFiles.ServiceFileName);

    // xdg-desktop-portal looks for "<desktop>-portals.conf" across every config
    // directory before it ever considers the generic "portals.conf" in any of
    // them, so a system-shipped desktop-specific file (e.g. hyprland-portals.conf)
    // silently shadows a claim written only to the generic name.
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

    // xdg-desktop-portal starts rove-portal on demand and it stays running
    // afterward, so uninstalling out from under it leaves an orphaned process
    // holding its own deleted binary open.
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

    // xdg-desktop-portal reads portals.conf/<desktop>-portals.conf once at its
    // own startup and never notices it change afterward, so a claim or revert
    // otherwise has no live effect until the user logs out and back in. It's
    // D-Bus-activatable, so killing it here is enough — the next portal
    // request from any app makes D-Bus relaunch it fresh, config and all.
    public static void RestartDesktopPortal()
    {
        if (FindProcessId("org.freedesktop.portal.Desktop") is not { } pid)
            return;
        try
        {
            Process.GetProcessById(pid).Kill();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
        }
    }

    private static int? FindProcessId(string busName)
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

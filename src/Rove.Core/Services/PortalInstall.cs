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
        bool changed = WriteIfDifferent(PortalFilePath(dataHome), PortalFiles.PortalFileContents());
        changed |= WriteIfDifferent(ServiceFilePath(dataHome), PortalFiles.ServiceFileContents(executable));
        if (changed)
            ReloadSessionBus();
    }

    public static void Withdraw(string dataHome)
    {
        bool changed = TryDelete(PortalFilePath(dataHome));
        changed |= TryDelete(ServiceFilePath(dataHome));
        if (changed)
            ReloadSessionBus();
    }

    public static PortalStatus CurrentStatus(string configPath, string statePath) =>
        PortalInstallState.Read(statePath) is not null ? PortalStatus.OwnedByRove
        : File.Exists(configPath) ? PortalStatus.OwnedByOther
        : PortalStatus.NotInstalled;

    public static bool EnsureBackend(string configPath, string statePath, string preferredName) =>
        CurrentStatus(configPath, statePath) == PortalStatus.NotInstalled
            && ClaimNow(configPath, statePath, preferredName);

    public static bool Enable(
        string dataHome, string configPath, string statePath, string executable, string preferredName)
    {
        Advertise(dataHome, executable);
        return CurrentStatus(configPath, statePath) != PortalStatus.OwnedByRove
            && ClaimNow(configPath, statePath, preferredName);
    }

    public static bool Disable(string dataHome, string configPath, string statePath)
    {
        bool reverted = RevertBackend(configPath, statePath);
        Withdraw(dataHome);
        return reverted;
    }

    public static bool RevertBackend(string configPath, string statePath)
    {
        PortalInstallState? state = PortalInstallState.Read(statePath);
        if (state is null)
            return false;

        try
        {
            if (state.PriorContent is null)
                File.Delete(configPath);
            else
            {
                if (Path.GetDirectoryName(configPath) is { Length: > 0 } parent)
                    Directory.CreateDirectory(parent);
                File.WriteAllText(configPath, state.PriorContent);
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
        return PortalInstallState.For(prior).Write(statePath) && WriteConfig(configPath, preferredName);
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

    private static bool WriteIfDifferent(string path, string contents)
    {
        try
        {
            if (File.Exists(path) && File.ReadAllText(path) == contents)
                return false;
            if (Path.GetDirectoryName(path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(path, contents);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // New service/portal files (or their removal) sit invisible to the running
    // session bus until it rescans: it only reads them at its own startup, so
    // an install done after login otherwise needs a logout to take effect.
    private static void ReloadSessionBus()
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

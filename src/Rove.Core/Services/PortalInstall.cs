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

    public static string ConfigPath(string configHome) =>
        Path.Combine(configHome, "xdg-desktop-portal", "portals.conf");

    public static void Advertise(string dataHome, string executable)
    {
        WriteIfDifferent(PortalFilePath(dataHome), PortalFiles.PortalFileContents());
        WriteIfDifferent(ServiceFilePath(dataHome), PortalFiles.ServiceFileContents(executable));
    }

    public static void Withdraw(string dataHome)
    {
        TryDelete(PortalFilePath(dataHome));
        TryDelete(ServiceFilePath(dataHome));
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

    private static void WriteIfDifferent(string path, string contents)
    {
        try
        {
            if (File.Exists(path) && File.ReadAllText(path) == contents)
                return;
            if (Path.GetDirectoryName(path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(path, contents);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

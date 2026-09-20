namespace Rove.Core.Services;

public static class RovePaths
{
    public const string AppFolderName = "rove";

    public static string ConfigDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, AppFolderName);
            }
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } dir
                ? dir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, AppFolderName);
        }
    }

    public static string KeybindingsFile => Path.Combine(ConfigDirectory, "keybindings.json");

    public static string BookmarksFile => Path.Combine(ConfigDirectory, "bookmarks.json");

    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");

    public static string CustomThemeFile => Path.Combine(ConfigDirectory, "theme.json");

    public static string DownloadsDirectory
    {
        get
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!OperatingSystem.IsWindows() && ReadXdgDownloadDir(home) is { } configured)
                return configured;
            return Path.Combine(home, "Downloads");
        }
    }

    private static string? ReadXdgDownloadDir(string home)
    {
        string userDirsFile = Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } dir
                ? dir
                : Path.Combine(home, ".config"),
            "user-dirs.dirs");
        try
        {
            if (!File.Exists(userDirsFile))
                return null;
            const string Prefix = "XDG_DOWNLOAD_DIR=";
            foreach (string line in File.ReadAllLines(userDirsFile))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
                    continue;
                string value = trimmed[Prefix.Length..].Trim('"');
                return value.Replace("$HOME", home, StringComparison.Ordinal).Replace("~", home, StringComparison.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    public static string StateDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, AppFolderName);
            }
            string stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME") is { Length: > 0 } dir
                ? dir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
            return Path.Combine(stateHome, AppFolderName);
        }
    }

    public static string InstallRecordFile => Path.Combine(StateDirectory, "installed.json");

    public static string LogDirectory => Path.Combine(StateDirectory, "logs");

    public static string PortalInstallStateFile => Path.Combine(StateDirectory, "portal-install.json");

    public static string PortalAskedFile => Path.Combine(StateDirectory, "portal-asked.json");

    public static string MimeDefaultStateFile => Path.Combine(StateDirectory, "mime-default.json");

    public static string UpdateCheckStateFile => Path.Combine(StateDirectory, "update-check.json");
}

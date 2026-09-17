namespace Rove.Core.Services;

/// <summary>
/// Where Rove keeps the files a user is meant to edit by hand. Windows uses
/// the roaming app-data folder, everything else follows the XDG convention
/// (<c>$XDG_CONFIG_HOME</c>, or <c>~/.config</c> when it isn't set).
/// </summary>
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

    /// <summary>Optional keybinding overrides. Absent means "use the defaults".</summary>
    public static string KeybindingsFile => Path.Combine(ConfigDirectory, "keybindings.json");

    /// <summary>The places the user asked Rove to remember. Absent means none yet.</summary>
    public static string BookmarksFile => Path.Combine(ConfigDirectory, "bookmarks.json");

    /// <summary>Rove's own preferences — theme, defaults, that sort of thing.</summary>
    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");

    public static string CustomThemeFile => Path.Combine(ConfigDirectory, "theme.json");

    /// <summary>
    /// Where downloaded files land. Linux honors a user's XDG_DOWNLOAD_DIR
    /// override (a localized folder name, or a different drive entirely);
    /// everything else falls back to the conventional "Downloads" under home.
    /// </summary>
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

    /// <summary>
    /// Where Rove keeps notes to itself — not settings, and nothing a person
    /// should have to look at. Windows uses the local (non-roaming) app-data
    /// folder; everything else follows XDG's state directory.
    /// </summary>
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

    /// <summary>What the last install put where — read at startup to skip doing it again.</summary>
    public static string InstallRecordFile => Path.Combine(StateDirectory, "installed.json");

    public static string SessionFile => Path.Combine(StateDirectory, "session.json");

    public static string LogDirectory => Path.Combine(StateDirectory, "logs");

    public static string PortalInstallStateFile => Path.Combine(StateDirectory, "portal-install.json");

    /// <summary>Marks that Rove has already offered to become the default file picker.</summary>
    public static string PortalAskedFile => Path.Combine(StateDirectory, "portal-asked.json");

    /// <summary>What mimeapps.list pointed at for folders before Rove claimed it.</summary>
    public static string MimeDefaultStateFile => Path.Combine(StateDirectory, "mime-default.json");

    public static string UpdateCheckStateFile => Path.Combine(StateDirectory, "update-check.json");
}

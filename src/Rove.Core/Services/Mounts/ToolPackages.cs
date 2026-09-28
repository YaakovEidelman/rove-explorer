namespace Rove.Core.Services;

public static class ToolPackages
{
    private sealed record Family(string[] Names, string[] Install, string[] UDisks, string[] Gio);

    private static readonly Family[] _families =
    [
        new(["arch"], ["sudo", "pacman", "-S", "--needed"],
            ["udisks2", "polkit"], ["glib2", "gvfs", "gvfs-mtp", "gvfs-smb"]),
        new(["debian", "ubuntu"], ["sudo", "apt-get", "install"],
            ["udisks2"], ["libglib2.0-bin", "gvfs", "gvfs-backends"]),
        new(["fedora", "rhel"], ["sudo", "dnf", "install"],
            ["udisks2"], ["glib2", "gvfs", "gvfs-mtp", "gvfs-smb"]),
        new(["suse", "opensuse"], ["sudo", "zypper", "install"],
            ["udisks2"], ["glib2-tools", "gvfs", "gvfs-backends"]),
    ];

    public static string[]? Command(string osRelease, MountTool tool)
    {
        foreach (string name in Names(osRelease))
        {
            if (_families.FirstOrDefault(f => f.Names.Contains(name)) is { } family)
                return [.. family.Install, .. tool == MountTool.UDisks ? family.UDisks : family.Gio];
        }
        return null;
    }

    public static bool IsReadOnlySystem(string osRelease) => Names(osRelease).Contains("steamos");

    private static List<string> Names(string osRelease)
    {
        string id = "";
        string like = "";
        foreach (string line in osRelease.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("ID=", StringComparison.Ordinal))
                id = Value(trimmed);
            else if (trimmed.StartsWith("ID_LIKE=", StringComparison.Ordinal))
                like = Value(trimmed);
        }
        return [.. $"{id} {like}".Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.ToLowerInvariant())
            .Select(name => name.StartsWith("opensuse", StringComparison.Ordinal) ? "opensuse" : name)];
    }

    private static string Value(string line) => line[(line.IndexOf('=') + 1)..].Trim().Trim('"', '\'');
}

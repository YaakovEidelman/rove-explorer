using System.Runtime.Versioning;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public static class DefaultFileManager
{
    private const string DefaultAppsSection = "[Default Applications]";
    private const string DirectoryKey = "inode/directory";
    private const string DirectoryPrefix = DirectoryKey + "=";

    public static string MimeAppsPath(string configHome) => Path.Combine(configHome, "mimeapps.list");

    public static PortalStatus CurrentStatus(string mimeAppsPath, string statePath) =>
        DefaultFileManagerState.Read(statePath) is not null ? PortalStatus.OwnedByRove
        : CurrentDefault(mimeAppsPath) is not null ? PortalStatus.OwnedByOther
        : PortalStatus.NotInstalled;

    public static bool Enable(string mimeAppsPath, string statePath, string desktopFileName) =>
        CurrentStatus(mimeAppsPath, statePath) != PortalStatus.OwnedByRove
            && Claim(mimeAppsPath, statePath, desktopFileName);

    public static bool Disable(string mimeAppsPath, string statePath)
    {
        DefaultFileManagerState? state = DefaultFileManagerState.Read(statePath);
        if (state is null)
            return false;
        if (!SetDefault(mimeAppsPath, state.PriorDefault))
            return false;

        DefaultFileManagerState.Delete(statePath);
        return true;
    }

    public static string? CurrentDefault(string mimeAppsPath)
    {
        List<string> lines = ReadLines(mimeAppsPath);
        (int start, int end) = FindSection(lines, DefaultAppsSection);
        int key = FindKey(lines, start, end);
        return key < 0 ? null : lines[key][DirectoryPrefix.Length..].Trim();
    }

    private static bool Claim(string mimeAppsPath, string statePath, string desktopFileName)
    {
        string? prior = CurrentDefault(mimeAppsPath);
        return DefaultFileManagerState.For(prior).Write(statePath) && SetDefault(mimeAppsPath, desktopFileName);
    }

    private static bool SetDefault(string mimeAppsPath, string? value)
    {
        try
        {
            List<string> lines = ReadLines(mimeAppsPath);
            (int start, int end) = FindSection(lines, DefaultAppsSection);
            int key = FindKey(lines, start, end);

            if (value is null)
            {
                if (key < 0)
                    return false;
                lines.RemoveAt(key);
            }
            else if (key >= 0)
                lines[key] = DirectoryPrefix + value;
            else if (start >= 0)
                lines.Insert(end, DirectoryPrefix + value);
            else
            {
                if (lines.Count > 0)
                    lines.Add("");
                lines.Add(DefaultAppsSection);
                lines.Add(DirectoryPrefix + value);
            }

            if (Path.GetDirectoryName(mimeAppsPath) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllLines(mimeAppsPath, lines);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static List<string> ReadLines(string path) =>
        File.Exists(path) ? [.. File.ReadAllLines(path)] : [];

    // mimeapps.list has other sections (Added/Removed Associations) that must
    // be left untouched, so edits are scoped to the one section's line range.
    private static (int Start, int End) FindSection(List<string> lines, string header)
    {
        int start = lines.FindIndex(l => l.Trim() == header);
        if (start < 0)
            return (-1, -1);
        int end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('['));
        return (start, end < 0 ? lines.Count : end);
    }

    private static int FindKey(List<string> lines, int start, int end)
    {
        if (start < 0)
            return -1;
        for (int i = start + 1; i < end; i++)
        {
            if (lines[i].StartsWith(DirectoryPrefix, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }
}

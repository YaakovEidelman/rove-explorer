using Rove.Core.Services;
using System;
using System.IO;

namespace Rove.UI.Services;

public static class DefaultConfigFiles
{
    public static void EnsureExist() =>
        EnsureExist(RovePaths.KeybindingsFile, RovePaths.BookmarksFile, RovePaths.CustomThemeFile);

    internal static void EnsureExist(string keybindingsPath, string bookmarksPath, string themePath)
    {
        Create(keybindingsPath, "{}\n");
        Create(bookmarksPath, "[]\n");
        Create(themePath, "{}\n");
    }

    private static void Create(string path, string content)
    {
        try
        {
            if (File.Exists(path))
                return;
            if (Path.GetDirectoryName(path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(path, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }
}

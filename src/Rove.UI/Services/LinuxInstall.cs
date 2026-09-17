using Rove.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace Rove.UI.Services;

/// <summary>
/// Puts Rove where a Linux desktop looks for an application: the build itself
/// in <c>~/.local/lib/rove</c> — a program and the libraries it draws with,
/// which belong together in a folder of their own rather than loose in a bin
/// directory — with <c>~/.local/bin/rove</c> pointing at it so the name works
/// in a terminal, a <c>rove.desktop</c> entry in
/// <c>~/.local/share/applications</c>, and its icons in the hicolor theme
/// beside it.
///
/// <para>
/// A window on Linux does not carry its own icon as far as most desktops are
/// concerned — the shell matches the window's <c>WM_CLASS</c> ("rove", pinned
/// in <c>Program.cs</c>) to a desktop entry and takes the icon from there. No
/// entry, no icon, anywhere: not in the dock, the switcher, or the app grid.
/// </para>
///
/// <para>
/// A packaged install (an entry in <c>/usr/share/applications</c>) owns all
/// of this, and Rove leaves it alone.
/// </para>
/// </summary>
[SupportedOSPlatform("linux")]
internal static class LinuxInstall
{
    public const string AppId = "rove";

    private const string DesktopFileName = AppId + ".desktop";

    /// <summary>What the published build calls its executable.</summary>
    private const string ExecutableName = "Rove";

    private static readonly int[] _iconSizes = [16, 24, 32, 48, 64, 128, 256, 512];

    /// <summary>rwxr-xr-x — what every other program in ~/.local/bin looks like.</summary>
    private const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    public static string BinDirectory => Path.Combine(XdgPaths.Home, ".local", "bin");

    public static string LibDirectory => Path.Combine(XdgPaths.Home, ".local", "lib", AppId);

    public static string DataHome => XdgPaths.DataHome;

    /// <summary>The data directories a distribution package would have installed into.</summary>
    public static IEnumerable<string> SystemDataDirs => XdgPaths.DataDirs().Skip(1);

    /// <summary>The program itself, inside its own folder.</summary>
    public static string BinaryPath(string libDir) => Path.Combine(libDir, ExecutableName);

    public static string PortalBinaryPath(string libDir) => Path.Combine(libDir, "rove-portal");

    /// <summary>The name on PATH, which is a link to the program.</summary>
    public static string LinkPath(string binDir) => Path.Combine(binDir, AppId);

    /// <summary>Everything the install puts down besides the binary itself.</summary>
    public static string[] SupportFiles(string dataHome, string binDir)
    {
        string theme = Path.Combine(dataHome, "icons", "hicolor");
        return
        [
            LinkPath(binDir),
            Path.Combine(dataHome, "applications", DesktopFileName),
            .. _iconSizes.Select(size => Path.Combine(theme, $"{size}x{size}", "apps", AppId + ".png")),
            Path.Combine(theme, "scalable", "apps", AppId + ".svg"),
            Path.Combine(theme, "symbolic", "apps", AppId + "-symbolic.svg"),
        ];
    }

    /// <summary>True when a distribution package already owns the desktop entry.</summary>
    public static bool OwnedBySystem(IEnumerable<string> systemDataDirs) =>
        systemDataDirs.Any(dir => File.Exists(Path.Combine(dir, "applications", DesktopFileName)));

    /// <summary>
    /// Copies the build the caller is running from into
    /// <c>~/.local/lib/rove</c>, makes it executable, and points
    /// <c>~/.local/bin/rove</c> at it. Returns the installed program, or null
    /// when nothing could be written.
    /// </summary>
    public static string? InstallPayload(string libDir, string binDir, string executable)
    {
        string target = BinaryPath(libDir);
        if (PathCompare.PathMatches(executable, target))
            return target;

        if (Path.GetDirectoryName(Path.GetFullPath(executable)) is not { Length: > 0 } source)
            return null;
        if (!Payload.Install(source, libDir, ExecutableName, [Path.GetFileName(PortalBinaryPath(libDir))]))
            return null;

        try
        {
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(target, ExecutableMode);
                string portalBinary = PortalBinaryPath(libDir);
                if (File.Exists(portalBinary))
                    File.SetUnixFileMode(portalBinary, ExecutableMode);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }

        LinkOnPath(binDir, target);
        return target;
    }

    /// <summary>
    /// <c>~/.local/bin/rove</c> is a link to the program, remade every time in
    /// case an older install left a copy of the whole binary sitting there.
    /// </summary>
    private static void LinkOnPath(string binDir, string binary)
    {
        string link = LinkPath(binDir);
        try
        {
            Directory.CreateDirectory(binDir);
            // Deleting first covers every case in one line: no link yet, a
            // link pointing somewhere stale, or a whole binary left in its
            // place by an older install. Deleting what is not there is fine,
            // and deleting a link never touches what it points at.
            File.Delete(link);
            File.CreateSymbolicLink(link, binary);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // No link is a smaller loss than no install: the desktop entry
            // points at the program itself, so only the terminal name is gone.
        }
    }

    /// <summary>
    /// Writes the desktop entry and the icons, pointing the entry at
    /// <paramref name="binary"/>. Only what is missing or out of date is
    /// touched, so a launch with nothing to do writes nothing.
    /// </summary>
    public static bool InstallSupport(string dataHome, string binDir, string binary, Func<string, byte[]?> asset)
    {
        LinkOnPath(binDir, binary);

        bool wrote = WriteIfDifferent(
            Path.Combine(dataHome, "applications", DesktopFileName),
            Encoding.UTF8.GetBytes(DesktopFileContents(binary)));

        string theme = Path.Combine(dataHome, "icons", "hicolor");
        foreach (int size in _iconSizes)
        {
            wrote |= CopyAsset(asset, $"png/rove-{size}.png",
                Path.Combine(theme, $"{size}x{size}", "apps", AppId + ".png"));
        }
        wrote |= CopyAsset(asset, "rove.svg", Path.Combine(theme, "scalable", "apps", AppId + ".svg"));
        wrote |= CopyAsset(asset, "rove-symbolic.svg",
            Path.Combine(theme, "symbolic", "apps", AppId + "-symbolic.svg"));

        return wrote;
    }

    /// <summary>Takes back everything the install put down, program included.</summary>
    public static void Uninstall(string dataHome, string libDir, string binDir)
    {
        UninstallSupport(dataHome, binDir);
        Payload.Remove(libDir);
    }

    /// <summary>The desktop entry, the icons and the link — everything but the build.</summary>
    public static void UninstallSupport(string dataHome, string binDir)
    {
        foreach (string file in SupportFiles(dataHome, binDir))
            TryDelete(file);
    }

    /// <summary>Drops the name on PATH, leaving the build itself alone.</summary>
    public static void RemoveLink(string binDir) => TryDelete(LinkPath(binDir));

    /// <summary>Removes an installed build: the folder it lives in, and the link to it.</summary>
    public static void RemoveInstalled(string libDir, string binDir)
    {
        TryDelete(LinkPath(binDir));
        Payload.Remove(libDir);
    }

    /// <summary>The entry every Linux desktop reads to know what this program is.</summary>
    internal static string DesktopFileContents(string executable) =>
        "[Desktop Entry]\n"
        + "Type=Application\n"
        + "Version=1.0\n"
        + "Name=Rove\n"
        + "GenericName=File Manager\n"
        + "Comment=Keyboard-driven file explorer\n"
        + $"Exec={QuoteExec(executable)} %F\n"
        + $"Icon={AppId}\n"
        + "Terminal=false\n"
        + "Categories=System;Utility;FileTools;FileManager;\n"
        + "MimeType=inode/directory;\n"
        + "Keywords=files;folders;explorer;manager;browser;\n"
        + "StartupNotify=true\n"
        + $"StartupWMClass={AppId}\n";

    /// <summary>
    /// An Exec line is read as a shell-ish command, so a path with a space in
    /// it has to be quoted, and the few characters quoting does not cover
    /// escaped.
    /// </summary>
    internal static string QuoteExec(string executable)
    {
        if (executable.Length == 0)
            return AppId;

        bool needsQuotes = executable.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '\\' or '$' or '`' or '%');
        if (!needsQuotes)
            return executable;

        StringBuilder quoted = new(executable.Length + 8);
        quoted.Append('"');
        foreach (char c in executable)
        {
            if (c is '"' or '\\' or '$' or '`')
                quoted.Append('\\');
            quoted.Append(c);
        }
        quoted.Append('"');
        return quoted.ToString();
    }

    private static bool CopyAsset(Func<string, byte[]?> asset, string name, string destination) =>
        asset(name) is { } bytes && WriteIfDifferent(destination, bytes);

    private static bool WriteIfDifferent(string path, byte[] contents)
    {
        try
        {
            if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(contents))
                return false;

            if (Path.GetDirectoryName(path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllBytes(path, contents);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
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

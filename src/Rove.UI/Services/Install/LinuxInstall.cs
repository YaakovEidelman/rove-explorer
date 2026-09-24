using Rove.Core.Services;
using System.Runtime.Versioning;
using System.Text;

namespace Rove.UI.Services;

[SupportedOSPlatform("linux")]
internal static class LinuxInstall
{
    public const string AppId = RoveLaunch.AppId;

    private const string DesktopFileName = AppId + ".desktop";

    private const string ExecutableName = "Rove";

    private static readonly int[] _iconSizes = [16, 24, 32, 48, 64, 128, 256, 512];

    private const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    public static string BinDirectory => RoveLaunch.BinDirectory;

    public static string LibDirectory => Path.Combine(XdgPaths.Home, ".local", "lib", AppId);

    public static string DataHome => XdgPaths.DataHome;

    public static IEnumerable<string> SystemDataDirs => XdgPaths.DataDirs().Skip(1);

    public static string BinaryPath(string libDir) => Path.Combine(libDir, ExecutableName);

    public static string PortalBinaryPath(string libDir) => Path.Combine(libDir, "rove-portal");

    public static string LinkPath(string binDir) => Path.Combine(binDir, AppId);

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

    public static bool OwnedBySystem(IEnumerable<string> systemDataDirs) =>
        systemDataDirs.Any(dir => File.Exists(Path.Combine(dir, "applications", DesktopFileName)));

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

    private static void LinkOnPath(string binDir, string binary)
    {
        string link = LinkPath(binDir);
        try
        {
            Directory.CreateDirectory(binDir);
            File.Delete(link);
            File.CreateSymbolicLink(link, binary);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }

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

    public static void Uninstall(string dataHome, string libDir, string binDir)
    {
        UninstallSupport(dataHome, binDir);
        Payload.Remove(libDir);
    }

    public static void UninstallSupport(string dataHome, string binDir)
    {
        foreach (string file in SupportFiles(dataHome, binDir))
            TryDelete(file);
    }

    public static void RemoveLink(string binDir) => TryDelete(LinkPath(binDir));

    public static void RemoveInstalled(string libDir, string binDir)
    {
        TryDelete(LinkPath(binDir));
        Payload.Remove(libDir);
    }

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

    private static bool WriteIfDifferent(string path, byte[] contents) =>
        AtomicFileWrite.WriteIfDifferent(path, contents);

    private static void TryDelete(string path) => AtomicFileWrite.TryDelete(path);
}

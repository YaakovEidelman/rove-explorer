using System.ComponentModel;
using System.Diagnostics;
using Rove.Core.Services;
using Tmds.DBus.Protocol;

namespace Rove.Portal;

sealed class FileManagerHandler : IPathMethodHandler
{
    public const string ObjectPath = "/org/freedesktop/FileManager1";
    private const string InterfaceName = "org.freedesktop.FileManager1";

    private static readonly ReadOnlyMemory<byte> InterfaceXml =
        """
        <interface name="org.freedesktop.FileManager1">
          <method name="ShowFolders">
            <arg type="as" name="URIs" direction="in"/>
            <arg type="s" name="StartupId" direction="in"/>
          </method>
          <method name="ShowItems">
            <arg type="as" name="URIs" direction="in"/>
            <arg type="s" name="StartupId" direction="in"/>
          </method>
          <method name="ShowItemProperties">
            <arg type="as" name="URIs" direction="in"/>
            <arg type="s" name="StartupId" direction="in"/>
          </method>
        </interface>

        """u8.ToArray();

    public string Path => ObjectPath;
    public bool HandlesChildPaths => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([InterfaceXml]);
            return default;
        }

        var request = context.Request;
        if (request.InterfaceAsString != InterfaceName)
            return default;

        Func<string, string?>? resolveTarget = request.MemberAsString switch
        {
            "ShowFolders" => ResolveFolderTarget,
            "ShowItems" => ResolveItemTarget,
            "ShowItemProperties" => ResolveItemTarget,
            _ => null,
        };
        if (resolveTarget is null)
            return default;

        var reader = request.GetBodyReader();
        string[] uris = reader.ReadArrayOfString();

        foreach (string folder in uris.Select(resolveTarget).OfType<string>().Distinct())
            LaunchRoveAt(folder);

        using var writer = context.CreateReplyWriter(string.Empty);
        context.Reply(writer.CreateMessage());
        return default;
    }

    internal static string? ResolveItemTarget(string uri) =>
        ToLocalPath(uri) is { } path ? ParentOrSelf(path) : null;

    internal static string? ResolveFolderTarget(string uri) =>
        ToLocalPath(uri) is { } path ? (Directory.Exists(path) ? path : ParentOrSelf(path)) : null;

    private static string ParentOrSelf(string path) =>
        System.IO.Path.GetDirectoryName(path) is { Length: > 0 } parent ? parent : path;

    internal static string? ToLocalPath(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) && parsed.Scheme == Uri.UriSchemeFile
            ? parsed.LocalPath
            : null;

    private static void LaunchRoveAt(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo(RoveLaunch.ExecutablePath())
            {
                ArgumentList = { folder },
                UseShellExecute = false,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
        }
    }
}

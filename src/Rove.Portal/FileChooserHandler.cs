using System.Text;
using Rove.Core.Services;
using Tmds.DBus.Protocol;

namespace Rove.Portal;

sealed class FileChooserHandler : IPathMethodHandler
{
    public const string ObjectPath = "/org/freedesktop/portal/desktop";
    private const string InterfaceName = "org.freedesktop.impl.portal.FileChooser";
    private const uint ResponseSuccess = 0;
    private const uint ResponseCancelled = 1;

    private static readonly Lazy<LinuxMimeDatabase> MimeDatabase = new(LinuxMimeDatabase.Load);

    private static readonly ReadOnlyMemory<byte> InterfaceXml =
        """
        <interface name="org.freedesktop.impl.portal.FileChooser">
          <method name="OpenFile">
            <arg type="o" name="handle" direction="in"/>
            <arg type="s" name="app_id" direction="in"/>
            <arg type="s" name="parent_window" direction="in"/>
            <arg type="s" name="title" direction="in"/>
            <arg type="a{sv}" name="options" direction="in"/>
            <arg type="u" name="response" direction="out"/>
            <arg type="a{sv}" name="results" direction="out"/>
          </method>
          <method name="SaveFile">
            <arg type="o" name="handle" direction="in"/>
            <arg type="s" name="app_id" direction="in"/>
            <arg type="s" name="parent_window" direction="in"/>
            <arg type="s" name="title" direction="in"/>
            <arg type="a{sv}" name="options" direction="in"/>
            <arg type="u" name="response" direction="out"/>
            <arg type="a{sv}" name="results" direction="out"/>
          </method>
          <method name="SaveFiles">
            <arg type="o" name="handle" direction="in"/>
            <arg type="s" name="app_id" direction="in"/>
            <arg type="s" name="parent_window" direction="in"/>
            <arg type="s" name="title" direction="in"/>
            <arg type="a{sv}" name="options" direction="in"/>
            <arg type="u" name="response" direction="out"/>
            <arg type="a{sv}" name="results" direction="out"/>
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

        Func<FileChooserCall, MethodContext, Task>? handler = request.MemberAsString switch
        {
            "OpenFile" => HandleOpenFileAsync,
            "SaveFile" => HandleSaveFileAsync,
            "SaveFiles" => HandleSaveFilesAsync,
            _ => null,
        };
        if (handler is null)
            return default;

        FileChooserCall call = FileChooserCall.Read(request);
        context.DisposesAsynchronously = true;
        _ = RunAndDisposeAsync(handler, call, context);
        return default;
    }

    private static async Task RunAndDisposeAsync(
        Func<FileChooserCall, MethodContext, Task> handler, FileChooserCall call, MethodContext context)
    {
        using (context)
        {
            try
            {
                await handler(call, context);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                context.ReplyError("org.freedesktop.DBus.Error.Failed", ex.Message);
            }
        }
    }

    private static Task HandleOpenFileAsync(FileChooserCall call, MethodContext context)
    {
        bool multiple = call.Options.TryGetValue("multiple", out var m) && m.GetBool();
        bool directory = call.Options.TryGetValue("directory", out var d) && d.GetBool();
        string folder = ReadFolderOption(call.Options) ?? HomeDirectory;
        ((string Name, string[] Patterns)[] filters, int selected) =
            directory ? ([], 0) : ResolveFilters(call.Options);

        return RunPickerAsync(context, call.Handle, folder, multiple, directory, filters, selected,
            call.ParentWindow, chosen => [.. chosen.Select(ToFileUri)]);
    }

    private static Task HandleSaveFileAsync(FileChooserCall call, MethodContext context)
    {
        string folder = ReadFolderOption(call.Options) ?? HomeDirectory;
        string name = call.Options.TryGetValue("current_name", out var n) ? n.GetString() : "Untitled";

        return RunPickerAsync(context, call.Handle, folder, multiple: false, directory: true, [], 0,
            call.ParentWindow,
            chosen => chosen is { Length: > 0 } ? [ToFileUri(System.IO.Path.Combine(chosen[0], name))] : null);
    }

    private static Task HandleSaveFilesAsync(FileChooserCall call, MethodContext context)
    {
        string folder = ReadFolderOption(call.Options) ?? HomeDirectory;
        string[] names = call.Options.TryGetValue("files", out var f)
            ? [.. f.GetArray<VariantValue>().Select(v => DecodePath(v.GetArray<byte>()))]
            : ["Untitled"];

        return RunPickerAsync(context, call.Handle, folder, multiple: false, directory: true, [], 0,
            call.ParentWindow,
            chosen => chosen is { Length: > 0 }
                ? [.. names.Select(name => ToFileUri(System.IO.Path.Combine(chosen[0], name)))]
                : null);
    }

    internal static ((string Name, string[] Patterns)[] Filters, int SelectedIndex) ResolveFilters(
        Dictionary<string, VariantValue> options)
    {
        VariantValue[] groups = options.TryGetValue("filters", out var f) ? f.GetArray<VariantValue>() : [];
        if (groups.Length == 0)
            return ([], 0);

        (string Name, string[] Patterns)[] filters = [.. groups.Select(ResolveFilter)];

        int selected = 0;
        if (options.TryGetValue("current_filter", out VariantValue current))
        {
            string currentName = current.GetItem(0).GetString();
            int index = Array.FindIndex(filters, group => group.Name == currentName);
            if (index >= 0)
                selected = index;
        }
        return (filters, selected);
    }

    private static (string Name, string[] Patterns) ResolveFilter(VariantValue group)
    {
        string name = group.GetItem(0).GetString();
        VariantValue[] patterns = group.GetItem(1).GetArray<VariantValue>();
        string[] globs =
        [
            .. patterns.Where(p => p.GetItem(0).GetUInt32() == 0).Select(p => p.GetItem(1).GetString()),
            .. patterns.Where(p => p.GetItem(0).GetUInt32() == 1)
                .SelectMany(p => MimeDatabase.Value.ExtensionsForMimePattern(p.GetItem(1).GetString()))
                .Select(ext => $"*.{ext}"),
        ];
        return (name, globs);
    }

    private static async Task RunPickerAsync(
        MethodContext context, string handle, string folder, bool multiple, bool directory,
        (string Name, string[] Patterns)[] filters, int selectedFilterIndex, string parentWindow,
        Func<string[], string[]?> buildUris)
    {
        using RovePickerSession session =
            RovePickerSession.Start(folder, multiple, directory, filters, selectedFilterIndex, parentWindow);
        context.Connection.AddMethodHandler(new PortalRequest(handle, session.Cancel));
        try
        {
            string[]? chosen = await session.WaitForResultAsync();
            string[]? uris = chosen is null ? null : buildUris(chosen);
            if (uris is null)
                ReplyCancelled(context);
            else
                Reply(context, uris);
        }
        finally
        {
            context.Connection.RemoveMethodHandler(handle);
        }
    }

    private static void Reply(MethodContext context, string[] uris)
    {
        var results = new Dictionary<string, VariantValue>
        {
            ["uris"] = VariantValue.Array(uris),
        };

        using var writer = context.CreateReplyWriter("ua{sv}");
        writer.WriteUInt32(ResponseSuccess);
        writer.WriteDictionary(results);
        context.Reply(writer.CreateMessage());
    }

    private static void ReplyCancelled(MethodContext context)
    {
        using var writer = context.CreateReplyWriter("ua{sv}");
        writer.WriteUInt32(ResponseCancelled);
        writer.WriteDictionary(new Dictionary<string, VariantValue>());
        context.Reply(writer.CreateMessage());
    }

    internal static string? ReadFolderOption(Dictionary<string, VariantValue> options) =>
        options.TryGetValue("current_folder", out var v) ? DecodePath(v.GetArray<byte>()) : null;

    internal static string DecodePath(byte[] bytes) =>
        Encoding.UTF8.GetString(bytes).TrimEnd('\0');

    internal static string ToFileUri(string path) => new Uri(path).AbsoluteUri;

    private static string HomeDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private readonly record struct FileChooserCall(string Handle, string AppId, string ParentWindow, string Title, Dictionary<string, VariantValue> Options)
    {
        public static FileChooserCall Read(Message message)
        {
            var reader = message.GetBodyReader();
            string handle = reader.ReadObjectPathAsString();
            string appId = reader.ReadString();
            string parentWindow = reader.ReadString();
            string title = reader.ReadString();
            var options = reader.ReadDictionaryOfStringToVariantValue();
            return new FileChooserCall(handle, appId, parentWindow, title, options);
        }
    }
}

using Tmds.DBus.Protocol;

namespace Rove.Core.Services;

public sealed partial class UDisksMounts
{
    private const string Service = "org.freedesktop.UDisks2";
    private const string Root = "/org/freedesktop/UDisks2";
    private const string NotAuthorized = "org.freedesktop.UDisks2.Error.NotAuthorized";
    private const string AlreadyMounted = "org.freedesktop.UDisks2.Error.AlreadyMounted";

    private static readonly string[] _watchedInterfaces =
        [UDisksObjects.BlockInterface, UDisksObjects.FilesystemInterface, UDisksObjects.EncryptedInterface];

    private readonly SemaphoreSlim _connecting = new(1, 1);
    private DBusConnection? _connection;

    private async Task<DBusConnection?> ConnectionAsync()
    {
        await _connecting.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is not null || _disposed || DBusAddress.System is not { } address)
                return _connection;
            DBusConnection connection = new(address);
            try
            {
                await connection.ConnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is DBusExceptionBase or IOException)
            {
                connection.Dispose();
                return null;
            }
            _connection = connection;
            return connection;
        }
        finally
        {
            _connecting.Release();
        }
    }

    private void Forget(DBusConnection connection)
    {
        if (Interlocked.CompareExchange(ref _connection, null, connection) == connection)
            connection.Dispose();
    }

    private static async Task<UDisksObjects> ObjectsAsync(DBusConnection connection)
    {
        MessageBuffer call;
        using (MessageWriter writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(Service, Root, "org.freedesktop.DBus.ObjectManager", "GetManagedObjects");
            call = writer.CreateMessage();
        }
        return UDisksObjects.Read(
            await connection.CallMethodAsync(call, static (message, _) => ReadManagedObjects(message), null)
                .ConfigureAwait(false));
    }

    private static Task CallAsync(DBusConnection connection, string path, string iface, string member) =>
        connection.CallMethodAsync(OptionsCall(connection, path, iface, member, text: null));

    private static Task<string> MountAsync(DBusConnection connection, string path) =>
        connection.CallMethodAsync(
            OptionsCall(connection, path, UDisksObjects.FilesystemInterface, "Mount", text: null),
            static (message, _) => message.GetBodyReader().ReadString(),
            null);

    private static Task<string> UnlockAsync(DBusConnection connection, string path, string passphrase) =>
        connection.CallMethodAsync(
            OptionsCall(connection, path, UDisksObjects.EncryptedInterface, "Unlock", passphrase),
            static (message, _) => message.GetBodyReader().ReadObjectPathAsString(),
            null);

    private static MessageBuffer OptionsCall(
        DBusConnection connection, string path, string iface, string member, string? text)
    {
        using MessageWriter writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            Service, path, iface, member, text is null ? "a{sv}" : "sa{sv}",
            MessageFlags.AllowInteractiveAuthorization);
        if (text is not null)
            writer.WriteString(text);
        writer.WriteDictionary(new Dictionary<string, VariantValue>());
        return writer.CreateMessage();
    }

    private static Dictionary<string, Dictionary<string, Dictionary<string, VariantValue>>> ReadManagedObjects(
        Message message)
    {
        Reader reader = message.GetBodyReader();
        Dictionary<string, Dictionary<string, Dictionary<string, VariantValue>>> objects = [];
        ArrayEnd objectsEnd = reader.ReadDictionaryStart();
        while (reader.HasNext(objectsEnd))
        {
            string path = reader.ReadObjectPathAsString();
            Dictionary<string, Dictionary<string, VariantValue>> interfaces = [];
            ArrayEnd interfacesEnd = reader.ReadDictionaryStart();
            while (reader.HasNext(interfacesEnd))
            {
                string name = reader.ReadString();
                interfaces[name] = reader.ReadDictionaryOfStringToVariantValue();
            }
            objects[path] = interfaces;
        }
        return objects;
    }

    private static bool IsMountChange(Message message) => message.MemberAsString switch
    {
        "InterfacesAdded" or "InterfacesRemoved" => true,
        "PropertiesChanged" => _watchedInterfaces.Contains(message.GetBodyReader().ReadString(), StringComparer.Ordinal),
        _ => false,
    };

    private static string ErrorText(DBusErrorReplyException ex, string fallback) =>
        ex.ErrorName.StartsWith(NotAuthorized, StringComparison.Ordinal)
            ? "Rove wasn't allowed to do that."
            : ex.ErrorMessage is { Length: > 0 } text ? text : fallback;
}

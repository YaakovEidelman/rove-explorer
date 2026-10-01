using System.Text;
using Tmds.DBus.Protocol;

namespace Rove.Core.Services;

public sealed partial class SecretServiceStore
{
    private const string Service = "org.freedesktop.secrets";
    private const string ServicePath = "/org/freedesktop/secrets";
    private const string ServiceInterface = "org.freedesktop.Secret.Service";
    private const string CollectionInterface = "org.freedesktop.Secret.Collection";
    private const string ItemInterface = "org.freedesktop.Secret.Item";
    private const string PromptInterface = "org.freedesktop.Secret.Prompt";
    private const string DefaultCollection = "/org/freedesktop/secrets/aliases/default";
    private const string NoPrompt = "/";
    private const string Schema = "io.github.rove.Server";

    private static readonly TimeSpan PromptWait = TimeSpan.FromMinutes(5);

    private static (string Name, string Value)[] Attributes(string key) =>
        [("xdg:schema", Schema), ("application", "rove"), ("server", key)];

    private static Task<string> OpenSessionAsync(DBusConnection connection)
    {
        MessageBuffer call;
        using (MessageWriter writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(Service, ServicePath, ServiceInterface, "OpenSession", "sv");
            writer.WriteString("plain");
            writer.WriteVariantString("");
            call = writer.CreateMessage();
        }
        return connection.CallMethodAsync(call, static (message, _) =>
        {
            Reader reader = message.GetBodyReader();
            reader.ReadVariantValue();
            return reader.ReadObjectPathAsString();
        }, null);
    }

    private static Task<string[]> SearchAsync(DBusConnection connection, string key)
    {
        MessageBuffer call;
        MessageWriter writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(Service, ServicePath, ServiceInterface, "SearchItems", "a{ss}");
            WriteAttributes(ref writer, key);
            call = writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
        return connection.CallMethodAsync(call, static (message, _) =>
        {
            Reader reader = message.GetBodyReader();
            string[] unlocked = reader.ReadArrayOfString();
            string[] locked = reader.ReadArrayOfString();
            return (string[])[.. unlocked, .. locked];
        }, null);
    }

    private static async Task<bool> UnlockAsync(DBusConnection connection, string[] paths)
    {
        MessageBuffer call;
        using (MessageWriter writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(Service, ServicePath, ServiceInterface, "Unlock", "ao");
            writer.WriteArray(paths);
            call = writer.CreateMessage();
        }
        string prompt = await connection.CallMethodAsync(call, static (message, _) =>
        {
            Reader reader = message.GetBodyReader();
            reader.ReadArrayOfString();
            return reader.ReadObjectPathAsString();
        }, null).ConfigureAwait(false);
        return prompt == NoPrompt || await PromptAsync(connection, prompt).ConfigureAwait(false);
    }

    private static Task<string> SecretOfAsync(DBusConnection connection, string item, string session)
    {
        MessageBuffer call;
        using (MessageWriter writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(Service, item, ItemInterface, "GetSecret", "o");
            writer.WriteObjectPath(session);
            call = writer.CreateMessage();
        }
        return connection.CallMethodAsync(call, static (message, _) =>
        {
            Reader reader = message.GetBodyReader();
            reader.AlignStruct();
            reader.ReadObjectPathAsString();
            reader.ReadArrayOfByte();
            byte[] value = reader.ReadArrayOfByte();
            return Encoding.UTF8.GetString(value);
        }, null);
    }

    private static Task<(string Item, string Prompt)> CreateItemAsync(
        DBusConnection connection, string session, string key, string label, string secret)
    {
        MessageBuffer call;
        MessageWriter writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(
                Service, DefaultCollection, CollectionInterface, "CreateItem", "a{sv}(oayays)b");
            ArrayStart properties = writer.WriteDictionaryStart();
            writer.WriteDictionaryEntryStart();
            writer.WriteString("org.freedesktop.Secret.Item.Label");
            writer.WriteVariantString(label);
            writer.WriteDictionaryEntryStart();
            writer.WriteString("org.freedesktop.Secret.Item.Attributes");
            writer.WriteSignature("a{ss}");
            WriteAttributes(ref writer, key);
            writer.WriteDictionaryEnd(properties);
            writer.WriteStructureStart();
            writer.WriteObjectPath(session);
            writer.WriteArray(Array.Empty<byte>());
            writer.WriteArray(Encoding.UTF8.GetBytes(secret));
            writer.WriteString("text/plain; charset=utf8");
            writer.WriteBool(true);
            call = writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
        return connection.CallMethodAsync(call, static (message, _) =>
        {
            Reader reader = message.GetBodyReader();
            return (reader.ReadObjectPathAsString(), reader.ReadObjectPathAsString());
        }, null);
    }

    private static Task<string> DeleteItemAsync(DBusConnection connection, string item)
    {
        MessageBuffer call;
        using (MessageWriter writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(Service, item, ItemInterface, "Delete");
            call = writer.CreateMessage();
        }
        return connection.CallMethodAsync(call, static (message, _) =>
            message.GetBodyReader().ReadObjectPathAsString(), null);
    }

    private static async Task<bool> PromptAsync(DBusConnection connection, string prompt)
    {
        TaskCompletionSource<bool> done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MatchRule rule = new()
        {
            Type = MessageType.Signal,
            Path = prompt,
            Interface = PromptInterface,
            Member = "Completed",
        };
        using IDisposable watch = await connection.AddMatchAsync(
            rule,
            static (message, _) => message.GetBodyReader().ReadBool(),
            (Notification<bool> completed) =>
            {
                if (completed.HasValue)
                    done.TrySetResult(!completed.Value);
                else
                    done.TrySetResult(false);
            },
            emitOnCapturedContext: false,
            ObserverFlags.None,
            null).ConfigureAwait(false);

        MessageBuffer call;
        using (MessageWriter writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(Service, prompt, PromptInterface, "Prompt", "s");
            writer.WriteString("");
            call = writer.CreateMessage();
        }
        await connection.CallMethodAsync(call).ConfigureAwait(false);
        try
        {
            return await done.Task.WaitAsync(PromptWait).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static void WriteAttributes(ref MessageWriter writer, string key)
    {
        ArrayStart start = writer.WriteDictionaryStart();
        foreach ((string name, string value) in Attributes(key))
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(name);
            writer.WriteString(value);
        }
        writer.WriteDictionaryEnd(start);
    }
}

using Tmds.DBus.Protocol;

namespace Rove.Core.Services;

public sealed partial class SecretServiceStore : ISecretStore, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DBusConnection? _connection;
    private string? _session;
    private bool _disposed;

    public Task<string?> LookupAsync(string key, CancellationToken ct) =>
        UseAsync<string?>(async (connection, session) =>
        {
            string[] items = await SearchAsync(connection, key).ConfigureAwait(false);
            if (items.Length == 0)
                return null;
            if (!await UnlockAsync(connection, items).ConfigureAwait(false))
                return null;
            return await SecretOfAsync(connection, items[0], session).ConfigureAwait(false);
        }, fallback: null, ct);

    public Task<bool> SaveAsync(string key, string label, string secret, CancellationToken ct) =>
        UseAsync(async (connection, session) =>
        {
            if (!await UnlockAsync(connection, [DefaultCollection]).ConfigureAwait(false))
                return false;
            (_, string prompt) = await CreateItemAsync(connection, session, key, label, secret).ConfigureAwait(false);
            return prompt == NoPrompt || await PromptAsync(connection, prompt).ConfigureAwait(false);
        }, fallback: false, ct);

    public Task<bool> DeleteAsync(string key, CancellationToken ct) =>
        UseAsync(async (connection, _) =>
        {
            bool all = true;
            foreach (string item in await SearchAsync(connection, key).ConfigureAwait(false))
            {
                string prompt = await DeleteItemAsync(connection, item).ConfigureAwait(false);
                if (prompt != NoPrompt)
                    all &= await PromptAsync(connection, prompt).ConfigureAwait(false);
            }
            return all;
        }, fallback: false, ct);

    public void Dispose()
    {
        _disposed = true;
        _connection?.Dispose();
        _connection = null;
    }

    private async Task<T> UseAsync<T>(
        Func<DBusConnection, string, Task<T>> work, T fallback, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (await SessionAsync().ConfigureAwait(false) is not ({ } connection, { } session))
                return fallback;
            try
            {
                return await work(connection, session).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is DBusExceptionBase or IOException or ObjectDisposedException)
            {
                Forget();
                return fallback;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(DBusConnection?, string?)> SessionAsync()
    {
        if (_connection is not null && _session is not null)
            return (_connection, _session);
        if (_disposed || DBusAddress.Session is not { Length: > 0 } address)
            return (null, null);

        DBusConnection connection = new(address);
        try
        {
            await connection.ConnectAsync().ConfigureAwait(false);
            _session = await OpenSessionAsync(connection).ConfigureAwait(false);
            _connection = connection;
            return (_connection, _session);
        }
        catch (Exception ex) when (ex is DBusExceptionBase or IOException)
        {
            connection.Dispose();
            return (null, null);
        }
    }

    private void Forget()
    {
        _connection?.Dispose();
        _connection = null;
        _session = null;
    }
}

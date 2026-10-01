using Rove.Core.Services;
using System.Text.Json;

namespace Rove.UI.Services;

public sealed class ServerStore
{
    private readonly string _path;
    private readonly List<SavedServer> _items;

    public event Action? Changed;

    public ServerStore() : this(RovePaths.ServersFile)
    {
    }

    public ServerStore(string path)
    {
        _path = path;
        _items = Read(path);
    }

    public IReadOnlyList<SavedServer> Items => _items;

    public SavedServer? Find(string address) => _items.Find(s => Same(s.Address, address));

    public void Save(SavedServer server, string? replacing = null)
    {
        int index = _items.FindIndex(s => Same(s.Address, replacing ?? server.Address));
        if (index < 0)
            index = _items.FindIndex(s => Same(s.Address, server.Address));
        if (index >= 0)
            _items[index] = server;
        else
            _items.Add(server);
        _items.RemoveAll(s => !ReferenceEquals(s, server) && Same(s.Address, server.Address));
        Write();
    }

    public bool Remove(string address)
    {
        if (_items.RemoveAll(s => Same(s.Address, address)) == 0)
            return false;
        Write();
        return true;
    }

    private static bool Same(string first, string second) =>
        string.Equals(first.Trim(), second.Trim(), StringComparison.Ordinal);

    private void Write()
    {
        Changed?.Invoke();
        try
        {
            if (Path.GetDirectoryName(_path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(_path, JsonSerializer.Serialize(_items, ServerJson.Default.ListSavedServer));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    private static List<SavedServer> Read(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];
            List<SavedServer>? read =
                JsonSerializer.Deserialize(File.ReadAllText(path), ServerJson.Default.ListSavedServer);
            return read is null ? [] : [.. read.Where(s => s is { Address.Length: > 0 })];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return [];
        }
    }
}

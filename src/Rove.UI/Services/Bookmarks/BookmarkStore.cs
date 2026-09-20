using Rove.Core.Services;
using System.Text.Json;

namespace Rove.UI.Services;

public sealed class BookmarkStore
{
    public const int ShortcutCount = 9;

    private readonly string _path;
    private readonly List<Bookmark> _items;

    public event Action? Changed;

    public BookmarkStore() : this(RovePaths.BookmarksFile)
    {
    }

    public BookmarkStore(string path)
    {
        _path = path;
        _items = Read(path);
    }

    public IReadOnlyList<Bookmark> Items => _items;

    public bool Contains(string path) => IndexOf(path) >= 0;

    public int IndexOf(string path) =>
        _items.FindIndex(b => PathCompare.PathMatches(b.Path, path));

    public Bookmark? At(int index) =>
        index >= 0 && index < _items.Count ? _items[index] : null;

    public static string ShortcutFor(int index) =>
        index >= 0 && index < ShortcutCount ? $"Ctrl+{index + 1}" : string.Empty;

    public bool Toggle(Bookmark bookmark)
    {
        int existing = IndexOf(bookmark.Path);
        if (existing >= 0)
        {
            _items.RemoveAt(existing);
            Save();
            return false;
        }
        _items.Add(bookmark);
        Save();
        return true;
    }

    public bool Remove(string path)
    {
        int existing = IndexOf(path);
        if (existing < 0)
            return false;
        _items.RemoveAt(existing);
        Save();
        return true;
    }

    private void Save()
    {
        Changed?.Invoke();
        try
        {
            if (System.IO.Path.GetDirectoryName(_path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(_path, JsonSerializer.Serialize(_items, BookmarkJson.Default.ListBookmark));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    private static List<Bookmark> Read(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];
            List<Bookmark>? read =
                JsonSerializer.Deserialize(File.ReadAllText(path), BookmarkJson.Default.ListBookmark);
            return read is null ? [] : [.. read.Where(b => b is { Path.Length: > 0 })];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return [];
        }
    }
}

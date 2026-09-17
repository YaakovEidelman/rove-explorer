using Rove.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.UI.Services;

/// <summary>
/// One remembered place. The name is only what to show — the path is the
/// bookmark, and a rename of the folder it points at is a broken bookmark
/// rather than a renamed one.
/// </summary>
public sealed record Bookmark(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isDirectory")] bool IsDirectory
);

/// <summary>
/// The places worth coming back to, in the order they were bookmarked.
///
/// <para>
/// There is no list on screen: bookmarks live in a file next to the
/// keybindings and are reached through the palette, or through the
/// first-nine shortcut each one gets by its position — so adding a tenth
/// never moves the nine already in the user's fingers.
/// </para>
/// </summary>
public sealed class BookmarkStore
{
    /// <summary>How many of them get a key of their own. The rest are still in the list.</summary>
    public const int ShortcutCount = 9;

    private readonly string _path;
    private readonly List<Bookmark> _items;

    /// <summary>Raised whenever the list changes, so a list on screen can follow.</summary>
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

    /// <summary>
    /// The keys the first few answer to. Position, not identity: the tenth
    /// bookmark has no shortcut until something ahead of it is removed.
    /// </summary>
    public static string ShortcutFor(int index) =>
        index >= 0 && index < ShortcutCount ? $"Ctrl+{index + 1}" : string.Empty;

    /// <summary>Adds it, or takes it out again when it is already there.</summary>
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
            // A bookmark that could not be written down still works for this
            // run; losing it at the next launch beats interrupting the user.
        }
    }

    /// <summary>Anything unreadable reads as no bookmarks rather than as an error.</summary>
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

/// <summary>
/// The file's shape, worked out at compile time: a natively compiled build
/// cannot reflect over a type to find its properties.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<Bookmark>))]
internal partial class BookmarkJson : JsonSerializerContext
{
}

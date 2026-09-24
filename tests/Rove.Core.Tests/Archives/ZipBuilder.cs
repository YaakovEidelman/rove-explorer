using System.IO.Compression;
using Rove.Core.Services;

namespace Rove.Core.Tests;

public static class ZipBuilder
{
    public static string Make(string path, params (string Name, string Content)[] entries)
    {
        using FileStream stream = new(LongPath.ForIo(path), FileMode.Create);
        using ZipArchive zip = new(stream, ZipArchiveMode.Create);
        foreach ((string name, string content) in entries)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);
            if (name.EndsWith('/'))
                continue;
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }
        return path;
    }
}

using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Rove.Core.Services;

namespace Rove.Core.Tests;

public static class TarBuilder
{
    public static string Make(string path, bool gzip, params (string Name, string Content)[] entries)
    {
        using FileStream file = new(LongPath.ForIo(path), FileMode.Create);
        using Stream output = gzip ? new GZipStream(file, CompressionLevel.Optimal) : file;
        using TarWriter writer = new(output);

        foreach ((string name, string content) in entries)
        {
            if (name.EndsWith('/'))
            {
                writer.WriteEntry(new PaxTarEntry(TarEntryType.Directory, name));
                continue;
            }

            PaxTarEntry entry = new(TarEntryType.RegularFile, name)
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            };
            writer.WriteEntry(entry);
        }
        return path;
    }
}

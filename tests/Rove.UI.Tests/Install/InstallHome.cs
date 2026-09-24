using System.Text;

namespace Rove.UI.Tests;

public sealed class InstallHome : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rove-install-" + Guid.NewGuid().ToString("N"));

    public string DataHome => Path.Combine(Root, "share");

    public string BinDir => Path.Combine(Root, "bin");

    public string LibDir => Path.Combine(Root, "lib");

    public string StartMenu => Path.Combine(Root, "start-menu");

    public string Sub(params string[] parts) => Path.Combine([Root, .. parts]);

    public string Build(string folder, string content = "a binary", string executable = "Rove")
    {
        string dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, executable), content);
        File.WriteAllText(Path.Combine(dir, "libSkiaSharp.so"), "skia " + content);
        File.WriteAllText(Path.Combine(dir, "libHarfBuzzSharp.so"), "harfbuzz");
        File.WriteAllText(Path.Combine(dir, "Rove.pdb"), "symbols nobody needs");
        return Path.Combine(dir, executable);
    }

    public static byte[]? Asset(string name) => Encoding.UTF8.GetBytes("asset:" + name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
        }
    }
}

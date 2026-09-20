using Rove.Core.Services;

namespace Rove.Core.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "rove-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string File(string name, string content = "")
    {
        string p = Sub(name);
        System.IO.File.WriteAllText(p, content);
        return p;
    }

    public string Dir(string name)
    {
        string p = Sub(name);
        Directory.CreateDirectory(p);
        return p;
    }

    public string Sub(params string[] parts) =>
        System.IO.Path.Combine([Path, .. parts.Select(Native)]);

    private static string Native(string part) => part
        .Replace('\\', System.IO.Path.DirectorySeparatorChar)
        .Replace('/', System.IO.Path.DirectorySeparatorChar);

    public string DeepDir(int minLength = LongPath.WindowsMaxPath + 40)
    {
        string p = Path;
        while (p.Length < minLength)
            p = System.IO.Path.Combine(p, new string('d', 40));
        Directory.CreateDirectory(LongPath.ForIo(p));
        return p;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(LongPath.ForIo(Path), recursive: true);
        }
        catch
        {
        }
    }
}

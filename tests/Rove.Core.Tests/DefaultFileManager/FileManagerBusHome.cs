namespace Rove.Core.Tests;

public sealed class FileManagerBusHome : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rove-filemanager1-" + Guid.NewGuid().ToString("N"));

    public string DataHome => Path.Combine(Root, "share");

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

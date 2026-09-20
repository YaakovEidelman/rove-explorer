namespace Rove.Core.Tests;

public sealed class MimeAppsHome : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rove-mime-" + Guid.NewGuid().ToString("N"));

    public string MimeAppsPath => Path.Combine(Root, "config", "mimeapps.list");

    public string StatePath => Path.Combine(Root, "state", "mime-default.json");

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

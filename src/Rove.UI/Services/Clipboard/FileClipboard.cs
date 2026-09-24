namespace Rove.UI.Services;

public class FileClipboard
{
    public ClipboardOp Op { get; private set; } = ClipboardOp.Copy;
    public IReadOnlyList<string> Paths { get; private set; } = [];
    public bool HasItems => Paths.Count > 0;
    public bool OneShot { get; private set; }
    public Guid? Token { get; private set; }

    public event Action? Changed;

    public void Set(ClipboardOp op, IReadOnlyList<string> paths, bool oneShot, Guid token)
    {
        Op = op;
        Paths = paths;
        OneShot = oneShot;
        Token = token;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Paths = [];
        OneShot = false;
        Token = null;
        Changed?.Invoke();
    }
}

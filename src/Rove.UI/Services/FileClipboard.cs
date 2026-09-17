using System;
using System.Collections.Generic;

namespace Rove.UI.Services;

public enum ClipboardOp
{
    Copy,
    Cut,
}

/// <summary>
/// Rove's internal file clipboard for copy/cut/paste of items.
/// (Text-path copies go to the system clipboard instead.)
/// </summary>
public class FileClipboard
{
    public ClipboardOp Op { get; private set; } = ClipboardOp.Copy;
    public IReadOnlyList<string> Paths { get; private set; } = [];
    public bool HasItems => Paths.Count > 0;

    public event Action? Changed;

    public void Set(ClipboardOp op, IReadOnlyList<string> paths)
    {
        Op = op;
        Paths = paths;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Paths = [];
        Changed?.Invoke();
    }
}

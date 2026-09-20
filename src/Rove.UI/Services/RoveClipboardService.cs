using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Rove.UI.Services;

public interface IRoveClipboardService
{
    Task CopyTextAsync(string text);

    Task SetFilesAsync(IReadOnlyList<string> paths, ClipboardOp op);

    Task<(IReadOnlyList<string> Paths, ClipboardOp Op)?> TryGetFilesAsync();
}

public class RoveClipboardService : IRoveClipboardService
{
    private static readonly DataFormat<byte[]> WindowsPreferredDropEffect =
        DataFormat.CreateBytesPlatformFormat("Preferred DropEffect");

    private static readonly DataFormat<string> GnomeCopiedFiles =
        DataFormat.CreateStringPlatformFormat("x-special/gnome-copied-files");

    private readonly Func<IClipboard?> _clipboard;
    private readonly Func<IStorageProvider?> _storageProvider;

    public RoveClipboardService(Func<IClipboard?> clipboard, Func<IStorageProvider?> storageProvider)
    {
        _clipboard = clipboard;
        _storageProvider = storageProvider;
    }

    public Task CopyTextAsync(string text)
    {
        IClipboard? cb = _clipboard();
        return cb is null ? Task.CompletedTask : cb.SetTextAsync(text);
    }

    public async Task SetFilesAsync(IReadOnlyList<string> paths, ClipboardOp op)
    {
        IClipboard? clipboard = _clipboard();
        IStorageProvider? storage = _storageProvider();
        if (clipboard is null || storage is null || paths.Count == 0)
            return;

        DataTransfer transfer = new();
        foreach (string path in paths)
        {
            IStorageItem? item = Directory.Exists(path)
                ? await storage.TryGetFolderFromPathAsync(path)
                : await storage.TryGetFileFromPathAsync(path);
            if (item is not null)
                transfer.Add(DataTransferItem.CreateFile(item));
        }

        if (OperatingSystem.IsWindows())
        {
            byte[] dropEffect = BitConverter.GetBytes(op == ClipboardOp.Cut ? 2 : 1);
            transfer.Add(DataTransferItem.Create(WindowsPreferredDropEffect, dropEffect));
        }
        else if (OperatingSystem.IsLinux())
        {
            string verb = op == ClipboardOp.Cut ? "cut" : "copy";
            string value = verb + "\n" + string.Join('\n', paths.Select(p => new Uri(p).AbsoluteUri));
            transfer.Add(DataTransferItem.Create(GnomeCopiedFiles, value));
        }

        await clipboard.SetDataAsync(transfer);
    }

    public async Task<(IReadOnlyList<string> Paths, ClipboardOp Op)?> TryGetFilesAsync()
    {
        IClipboard? clipboard = _clipboard();
        if (clipboard is null)
            return null;

        IStorageItem[]? items = await clipboard.TryGetFilesAsync();
        if (items is null || items.Length == 0)
            return null;

        List<string> paths = [];
        foreach (IStorageItem item in items)
        {
            if (item.TryGetLocalPath() is { } path)
                paths.Add(path);
            item.Dispose();
        }
        if (paths.Count == 0)
            return null;

        ClipboardOp op = ClipboardOp.Copy;
        if (OperatingSystem.IsWindows())
        {
            byte[]? dropEffect = await clipboard.TryGetValueAsync(WindowsPreferredDropEffect);
            if (dropEffect is { Length: >= 4 } && BitConverter.ToInt32(dropEffect, 0) == 2)
                op = ClipboardOp.Cut;
        }

        return (paths, op);
    }
}

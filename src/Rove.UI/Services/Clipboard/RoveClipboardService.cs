using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace Rove.UI.Services;

public class RoveClipboardService : IRoveClipboardService
{
    private static readonly DataFormat<byte[]> WindowsPreferredDropEffect =
        DataFormat.CreateBytesPlatformFormat("Preferred DropEffect");

    private static readonly DataFormat<string> GnomeCopiedFiles =
        DataFormat.CreateStringPlatformFormat("x-special/gnome-copied-files");

    private static readonly DataFormat<byte[]> RoveClipboardToken =
        DataFormat.CreateBytesPlatformFormat("application/x-rove-clipboard-token");

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

    public async Task SetFilesAsync(IReadOnlyList<string> paths, ClipboardOp op, Guid token)
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
        transfer.Add(DataTransferItem.Create(RoveClipboardToken, token.ToByteArray()));

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

    public async Task<(IReadOnlyList<string> Paths, ClipboardOp Op, Guid? Token)?> TryGetFilesAsync()
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

        byte[]? tokenBytes = await clipboard.TryGetValueAsync(RoveClipboardToken);
        Guid? token = tokenBytes is { Length: 16 } ? new Guid(tokenBytes) : null;

        return (paths, op, token);
    }

    public Task ClearAsync()
    {
        IClipboard? clipboard = _clipboard();
        return clipboard is null ? Task.CompletedTask : clipboard.ClearAsync();
    }
}

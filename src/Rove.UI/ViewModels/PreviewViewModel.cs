using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core;
using Rove.Core.Protocol;
using Rove.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rove.UI.ViewModels;

public record MetaRow(string Label, string Value);

public partial class PreviewViewModel : ViewModelBase
{
    private const int TextPreviewBytes = 16 * 1024;
    private const long MaxPreviewFileBytes = 4 * 1024 * 1024;
    private const long MaxPreviewImageBytes = 64 * 1024 * 1024;

    private const int ImagePreviewWidth = 288;

    private readonly RoveCore _core;
    private readonly IImagePreviewLoader _images;
    private CancellationTokenSource? _cts;
    private FolderItem? _current;
    private bool _currentViaAdmin;

    public PreviewViewModel(CommandRegistry registry, RoveCore core, IImagePreviewLoader images)
    {
        _core = core;
        _images = images;
        registry.Register(CommandDef.TogglePreview, Toggle);
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _kind = string.Empty;

    [ObservableProperty]
    private ObservableCollection<MetaRow> _rows = [];

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviewBody))]
    private bool _hasTextPreview;

    [ObservableProperty]
    private IImage? _previewImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviewBody))]
    private bool _hasImagePreview;

    [ObservableProperty]
    private bool _isLoading;

    public bool HasPreviewBody => HasTextPreview || HasImagePreview;

    public void Toggle()
    {
        IsOpen = !IsOpen;
        if (IsOpen && _current is not null)
            _ = LoadAsync(_current, _currentViaAdmin);
    }

    public void ShowFor(FolderItem? item, bool viaAdmin = false)
    {
        _current = item;
        _currentViaAdmin = viaAdmin;
        if (!IsOpen)
            return;
        if (item is null)
        {
            ClearDisplay();
            return;
        }
        _ = LoadAsync(item, viaAdmin);
    }

    private void ClearDisplay()
    {
        Title = string.Empty;
        Kind = string.Empty;
        Rows = [];
        ClearBody();
    }

    private void ClearBody()
    {
        PreviewText = string.Empty;
        HasTextPreview = false;
        PreviewImage = null;
        HasImagePreview = false;
    }

    private async Task LoadAsync(FolderItem item, bool viaAdmin)
    {
        _cts?.Cancel();
        CancellationTokenSource cts = new();
        _cts = cts;

        Title = item.Name;
        Kind = item.IsDirectory
            ? "Folder"
            : string.IsNullOrEmpty(item.Extension) ? "File" : $"{item.Extension.TrimStart('.').ToUpperInvariant()} file";
        IsLoading = true;
        ClearBody();

        try
        {
            if (viaAdmin)
            {
                Rows = [.. BuildAdminRows(item)];
                if (!item.IsDirectory)
                    await LoadAdminBodyAsync(item, cts.Token);
                return;
            }

            CommandResult<ItemMetadata?> result =
                await _core.Actions.GetMetadataAsync(new(item.FullPath), cts.Token);
            if (cts.Token.IsCancellationRequested)
                return;

            if (!result.IsOk || result.Data is null)
            {
                Rows = [new MetaRow("Error", result.Message ?? "Could not read metadata.")];
                return;
            }
            Rows = [.. BuildRows(result.Data)];

            if (!item.IsDirectory)
                await LoadBodyAsync(item, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                IsLoading = false;
        }
    }

    private async Task LoadBodyAsync(FolderItem item, CancellationToken ct)
    {
        long size = item.Size ?? 0;

        if (_images.CanPreview(item.Extension))
        {
            if (size > MaxPreviewImageBytes)
                return;
            IImage? image = await _images.LoadAsync(item.FullPath, ImagePreviewWidth, ct);
            if (ct.IsCancellationRequested || image is null)
                return;
            PreviewImage = image;
            HasImagePreview = true;
            return;
        }

        if (size > MaxPreviewFileBytes)
            return;

        string? text = await TryReadTextAsync(item.FullPath, ct);
        if (ct.IsCancellationRequested || text is null)
            return;
        PreviewText = text;
        HasTextPreview = true;
    }

    private static MetaRow[] BuildAdminRows(FolderItem item)
    {
        List<MetaRow> rows = [new("Location", item.FullPath)];
        if (item.Size is { } size)
            rows.Add(new("Size", $"{ColumnDefaults.FormatSize(size)} ({size:N0} bytes)"));
        rows.Add(new("Modified", item.LastWriteTime.ToString("g")));
        rows.Add(new("Access", "administrator (read-only)"));
        return [.. rows];
    }

    private async Task LoadAdminBodyAsync(FolderItem item, CancellationToken ct)
    {
        if (_core.Admin is not { } admin)
            return;

        bool isImage = _images.CanPreview(item.Extension);
        long size = item.Size ?? 0;
        if (size > (isImage ? MaxPreviewImageBytes : MaxPreviewFileBytes))
            return;

        CommandResult<string> copy = await admin.CopyToTempAsync(
            item.FullPath, isImage ? MaxPreviewImageBytes : TextPreviewBytes, ct);
        if (!copy.IsOk || copy.Data is null)
        {
            Rows = [.. Rows, new MetaRow("Error", copy.Message ?? "Could not read the file.")];
            return;
        }

        try
        {
            if (ct.IsCancellationRequested)
                return;
            await LoadBodyAsync(FolderItem.FromPath(copy.Data), ct);
        }
        finally
        {
            admin.Discard(copy.Data);
        }
    }

    private static MetaRow[] BuildRows(ItemMetadata m)
    {
        var rows = new System.Collections.Generic.List<MetaRow>
        {
            new("Location", m.FullPath),
        };

        if (m.IsDirectory)
        {
            string suffix = m.Truncated ? "+" : "";
            rows.Add(new("Contains", $"{m.FileCount}{suffix} files, {m.DirectoryCount}{suffix} folders"));
            if (m.TotalSizeBytes is { } total)
                rows.Add(new("Size", $"{ColumnDefaults.FormatSize(total)}{suffix}"));
        }
        else if (m.SizeBytes is { } size)
        {
            rows.Add(new("Size", $"{ColumnDefaults.FormatSize(size)} ({size:N0} bytes)"));
        }

        rows.Add(new("Modified", m.ModifiedUtc.ToLocalTime().ToString("g")));
        rows.Add(new("Created", m.CreatedUtc.ToLocalTime().ToString("g")));
        rows.Add(new("Accessed", m.AccessedUtc.ToLocalTime().ToString("g")));

        if (m.UnixMode is { } mode)
            rows.Add(new("Permissions", FormatUnixMode(mode)));

        var attrs = new System.Collections.Generic.List<string>();
        if (m.IsReadOnly) attrs.Add("Read-only");
        if (m.IsHidden) attrs.Add("Hidden");
        if (m.IsSystem) attrs.Add("System");
        if (m.IsReparsePoint) attrs.Add("Link");
        if (attrs.Count > 0)
            rows.Add(new("Attributes", string.Join(", ", attrs)));
        if (m.LinkTarget is not null)
            rows.Add(new("Target", m.LinkTarget));

        return [.. rows];
    }

    private static string FormatUnixMode(UnixFileMode mode)
    {
        static char Bit(UnixFileMode mode, UnixFileMode flag, char c) => mode.HasFlag(flag) ? c : '-';
        string bits =
            $"{Bit(mode, UnixFileMode.UserRead, 'r')}{Bit(mode, UnixFileMode.UserWrite, 'w')}{Bit(mode, UnixFileMode.UserExecute, 'x')}" +
            $"{Bit(mode, UnixFileMode.GroupRead, 'r')}{Bit(mode, UnixFileMode.GroupWrite, 'w')}{Bit(mode, UnixFileMode.GroupExecute, 'x')}" +
            $"{Bit(mode, UnixFileMode.OtherRead, 'r')}{Bit(mode, UnixFileMode.OtherWrite, 'w')}{Bit(mode, UnixFileMode.OtherExecute, 'x')}";
        string octal = Convert.ToString((int)mode & 0x1FF, 8).PadLeft(3, '0');
        return $"{bits} ({octal})";
    }

    private static async Task<string?> TryReadTextAsync(string path, CancellationToken ct)
    {
        try
        {
            await using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] buffer = new byte[Math.Min(TextPreviewBytes, (int)Math.Min(fs.Length, int.MaxValue))];
            int read = await fs.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0)
                return "";
            if (buffer.Take(read).Any(b => b == 0))
                return null;
            return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

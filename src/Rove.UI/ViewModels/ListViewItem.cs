using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Protocol;
using Rove.UI.Services;
using System.Threading.Tasks;

namespace Rove.UI.ViewModels;

public partial class ListViewItem : ObservableObject
{
    private readonly IIconCache _cache;

    [ObservableProperty]
    private IImage? _icon;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _editText = string.Empty;

    [ObservableProperty]
    private FolderItem _item;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private bool _isMarked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowOpacity))]
    private bool _isCut;

    public double RowOpacity => IsCut ? 0.45 : 1.0;

    public string TypeText => Item.IsDirectory
        ? "Folder"
        : string.IsNullOrEmpty(Item.Extension) ? "File" : Item.Extension.TrimStart('.').ToUpperInvariant();

    public string SizeText => ColumnDefaults.FormatSize(Item.Size);

    public string ModifiedText => Item.LastWriteTime.ToString("g");

    partial void OnItemChanged(FolderItem value)
    {
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(ModifiedText));
    }

    public ListViewItem(FolderItem item, int size, IIconCache cache)
    {
        _item = item;
        _name = item.Name;
        _editText = item.Name;
        _cache = cache;
        RequestIcon(size);
    }

    public void UpdateData(FolderItem item, int size = 16)
    {
        Item = item;
        Name = item.Name;
        EditText = item.Name;
        RequestIcon(size);
    }

    public void SetIconSize(int size) => RequestIcon(size);

    private void RequestIcon(int size)
    {
        if (_cache.TryGetIcon(Item, size, out IImage? cached))
        {
            Icon = cached;
            return;
        }
        _ = LoadIconAsync(size);
    }

    private async Task LoadIconAsync(int size)
    {
        IImage? icon = await _cache.GetIconAsync(Item, size).ConfigureAwait(false);
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            Icon = icon;
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Icon = icon);
    }
}

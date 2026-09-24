using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Protocol;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class SearchResultItem : ObservableObject
{
    public FolderItem Item { get; }
    public string Name => Item.Name;
    public string Location { get; }

    [ObservableProperty]
    private IImage? _icon;

    public SearchResultItem(FolderItem item, string root, IIconCache cache)
    {
        Item = item;
        string? dir = Path.GetDirectoryName(item.FullPath);
        Location = dir is not null && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? "." + dir[root.Length..].TrimEnd(Path.DirectorySeparatorChar)
            : dir ?? "";
        _ = LoadIconAsync(cache);
    }

    private async Task LoadIconAsync(IIconCache cache)
    {
        Icon = await cache.GetIconAsync(Item, 16);
    }
}

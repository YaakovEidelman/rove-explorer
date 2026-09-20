namespace Rove.UI.Models;

public static class IconSizes
{
    public static int PixelsFor(IconSize size) => size switch
    {
        IconSize.Small => 48,
        IconSize.Medium => 72,
        IconSize.Large => 96,
        _ => 48,
    };

    public static double CellWidth(IconSize size) => PixelsFor(size) + 40;

    public static double CellHeight(IconSize size) => PixelsFor(size) + 44;
}

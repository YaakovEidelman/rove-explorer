namespace Rove.UI.ViewModels;

public sealed record PaletteRow(string? Header, PaletteEntry? Entry)
{
    public bool IsHeader => Header is not null;

    public bool IsSelectable => Entry is { IsAvailable: true };
}

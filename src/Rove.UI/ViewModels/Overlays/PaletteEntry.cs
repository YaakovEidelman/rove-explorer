using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public record PaletteEntry(Command Command, string Hint, IReadOnlyList<TitleSegment> TitleSegments, bool IsAvailable);

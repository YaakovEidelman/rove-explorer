namespace Rove.UI.Services;

public sealed record UndoStep(UndoAction Action, string Description, IReadOnlyList<PathPair> Items);

namespace Rove.UI.Services;

public sealed record KeymapLoad(IReadOnlyList<KeymapOverride> Overrides, IReadOnlyList<string> Problems)
{
    public static KeymapLoad Empty { get; } = new([], []);

    public string? Summary => Problems.Count == 0
        ? null
        : $"keybindings.json: {Problems[0]}" + (Problems.Count > 1 ? $" (+{Problems.Count - 1} more)" : "");
}

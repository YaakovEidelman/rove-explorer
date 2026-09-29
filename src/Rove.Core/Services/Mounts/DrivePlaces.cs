namespace Rove.Core.Services;

public sealed class DrivePlaces
{
    private (string Root, string Label)[] _all = [];

    public IReadOnlyList<(string Root, string Label)> All => _all;

    public event Action? Changed;

    public void Set(IEnumerable<(string Root, string Label)> places)
    {
        (string Root, string Label)[] next = [.. places];
        if (next.SequenceEqual(_all))
            return;
        _all = next;
        Changed?.Invoke();
    }

    public bool IsDriveRoot(string path) => _all.Any(place => PathCompare.PathMatches(place.Root, path));
}

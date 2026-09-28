namespace Rove.Core.Services;

public sealed class DrivePlaces
{
    private (string Root, string Label)[] _all = [];

    public IReadOnlyList<(string Root, string Label)> All => _all;

    public void Set(IEnumerable<(string Root, string Label)> places) => _all = [.. places];

    public bool IsDriveRoot(string path) => _all.Any(place => PathCompare.PathMatches(place.Root, path));
}

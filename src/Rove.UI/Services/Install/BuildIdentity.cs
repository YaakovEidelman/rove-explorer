namespace Rove.UI.Services;

public readonly record struct BuildIdentity(Version Version, long Size, DateTime ModifiedUtc)
{
    private static readonly TimeSpan _timeSlack = TimeSpan.FromSeconds(2);

    public static BuildIdentity? Of(string path, Version version)
    {
        try
        {
            FileInfo file = new(path);
            return file.Exists ? new BuildIdentity(version, file.Length, file.LastWriteTimeUtc) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public bool IsSameBuildAs(BuildIdentity other) =>
        Version == other.Version
        && Size == other.Size
        && (ModifiedUtc - other.ModifiedUtc).Duration() <= _timeSlack;

    public bool IsNewerThan(BuildIdentity other) =>
        Version != other.Version
            ? Version > other.Version
            : ModifiedUtc > other.ModifiedUtc + _timeSlack;
}

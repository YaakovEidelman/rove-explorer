namespace Rove.Core.Services;

public sealed record MountEntry(
    string Name,
    MountKind Kind,
    string? Device,
    string? ActivationUri,
    string? MountUri,
    string? LocalPath,
    bool CanMount,
    bool CanUnmount,
    bool CanEject
)
{
    public bool IsMounted => MountUri is not null;

    public bool IsFileBacked => MountUri?.StartsWith("file://", StringComparison.Ordinal) == true;

    public string Key => Device ?? ActivationUri ?? MountUri ?? Name;

    public bool SameVolume(MountEntry other) =>
        (Device is not null && Device == other.Device)
        || (ActivationUri is not null && ActivationUri == other.ActivationUri)
        || (MountUri is not null && MountUri == other.MountUri);
}

namespace Rove.Core.Services;

public static class SecretStoreChooser
{
    public static ISecretStore? CreateForHost() =>
        OperatingSystem.IsLinux() && DBusSessionAvailable() ? new SecretServiceStore() : null;

    private static bool DBusSessionAvailable() => Tmds.DBus.Protocol.DBusAddress.Session is { Length: > 0 };
}

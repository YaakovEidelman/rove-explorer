namespace Rove.Core.Services;

public static class PortalFiles
{
    public const string BusName = "org.freedesktop.impl.portal.desktop.rove";

    public const string ServiceFileName = BusName + ".service";

    public const string PortalFileName = "org.freedesktop.impl.portal.Rove.portal";

    public static string PreferredName => Path.GetFileNameWithoutExtension(PortalFileName);

    public static string ServiceFileContents(string executable) =>
        "[D-BUS Service]\n"
        + $"Name={BusName}\n"
        + $"Exec={executable}\n";

    public static string PortalFileContents() =>
        "[portal]\n"
        + $"DBusName={BusName}\n"
        + "Interfaces=org.freedesktop.impl.portal.FileChooser\n"
        + "UseIn=\n";
}

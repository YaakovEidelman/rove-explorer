namespace Rove.Core.Services;

public static class FileManagerBusFiles
{
    public const string BusName = "org.freedesktop.FileManager1";

    public const string ServiceFileName = BusName + ".service";

    public static string ServiceFileContents(string executable) =>
        "[D-BUS Service]\n"
        + $"Name={BusName}\n"
        + $"Exec={executable}\n";
}

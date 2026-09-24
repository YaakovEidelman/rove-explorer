using Rove.Core.Services;
using Rove.Portal;
using Tmds.DBus.Protocol;

string executable = Environment.ProcessPath ?? "rove-portal";
PortalInstall.Advertise(XdgPaths.DataHome, executable);

var connection = new DBusConnection(DBusAddress.Session!);
await connection.ConnectAsync();
connection.AddMethodHandler(new FileChooserHandler());
await connection.TryRequestNameAsync(PortalFiles.BusName, RequestNameOptions.None);

connection.AddMethodHandler(new FileManagerHandler());
await connection.TryRequestNameAsync(FileManagerBusFiles.BusName, RequestNameOptions.None);

Console.WriteLine($"rove-portal listening as {PortalFiles.BusName} at {FileChooserHandler.ObjectPath}");

var exit = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    exit.TrySetResult();
};
await exit.Task;

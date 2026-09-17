using Tmds.DBus.Protocol;

namespace Rove.Portal;

sealed class PortalRequest : IPathMethodHandler
{
    private const string InterfaceName = "org.freedesktop.impl.portal.Request";

    private static readonly ReadOnlyMemory<byte> InterfaceXml =
        """
        <interface name="org.freedesktop.impl.portal.Request">
          <method name="Close"/>
        </interface>

        """u8.ToArray();

    private readonly Action _onClose;

    public PortalRequest(string path, Action onClose)
    {
        Path = path;
        _onClose = onClose;
    }

    public string Path { get; }

    public bool HandlesChildPaths => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([InterfaceXml]);
            return default;
        }

        var request = context.Request;
        if (request.InterfaceAsString == InterfaceName && request.MemberAsString == "Close")
        {
            _onClose();
            using var writer = context.CreateReplyWriter(string.Empty);
            context.Reply(writer.CreateMessage());
        }

        return default;
    }
}

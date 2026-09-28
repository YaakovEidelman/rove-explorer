using System.Text.RegularExpressions;

namespace Rove.Core.Services;

public static partial class MountAddress
{
    [GeneratedRegex(@"^(?<scheme>[a-zA-Z][a-zA-Z0-9+.\-]*)://\S")]
    private static partial Regex Remote();

    public static bool LooksRemote(string text) =>
        Remote().Match(text.Trim()) is { Success: true } match
        && !match.Groups["scheme"].Value.Equals("file", StringComparison.OrdinalIgnoreCase);

    public static bool HasUser(string address) =>
        Uri.TryCreate(address.Trim(), UriKind.Absolute, out Uri? uri) && uri.UserInfo.Length > 0;

    public static string? LocalPathOfFileUri(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) && parsed.IsFile ? parsed.LocalPath : null;
}

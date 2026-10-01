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

    public static string? UserOf(string address) =>
        Uri.TryCreate(address.Trim(), UriKind.Absolute, out Uri? uri) && uri.UserInfo.Length > 0
            ? Uri.UnescapeDataString(uri.UserInfo.Split(':')[0])
            : null;

    public static string WithUser(string address, string? user)
    {
        string trimmed = address.Trim();
        if (user is not { Length: > 0 } name || HasUser(trimmed)
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out _))
            return trimmed;
        int start = trimmed.IndexOf("://", StringComparison.Ordinal) + 3;
        return $"{trimmed[..start]}{Uri.EscapeDataString(name)}@{trimmed[start..]}";
    }

    public static string ShortName(string address)
    {
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out Uri? uri) || uri.Host.Length == 0)
            return address.Trim();
        return UserOf(address) is { } user ? $"{user}@{uri.Host}" : uri.Host;
    }

    public static bool SameServer(string first, string second)
    {
        if (!Uri.TryCreate(first.Trim(), UriKind.Absolute, out Uri? a)
            || !Uri.TryCreate(second.Trim(), UriKind.Absolute, out Uri? b))
            return false;
        if (!a.Scheme.Equals(b.Scheme, StringComparison.OrdinalIgnoreCase)
            || !a.Host.Equals(b.Host, StringComparison.OrdinalIgnoreCase)
            || a.Port != b.Port)
            return false;
        return UserOf(first) is not { } left || UserOf(second) is not { } right || left == right;
    }
}

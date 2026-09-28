using System.Text.RegularExpressions;

namespace Rove.Core.Services;

public static partial class GioMountList
{
    private static readonly string[] _phoneSchemes = ["mtp://", "gphoto2://", "afc://"];

    [GeneratedRegex(@"^(?<indent> *)(?<kind>Drive|Volume|Mount)\(\d+\): (?<name>.*)$")]
    private static partial Regex Header();

    [GeneratedRegex(@"^ *(?<key>[a-z_\-]+)(=(?<value>.*)|: '(?<quoted>.*)')$")]
    private static partial Regex Property();

    private sealed class Node(string kind, int indent, string name, Node? parent)
    {
        public string Kind { get; } = kind;
        public int Indent { get; } = indent;
        public string Name { get; } = name;
        public Node? Parent { get; } = parent;
        public Dictionary<string, string> Props { get; } = [];
        public List<Node> Children { get; } = [];

        public bool Flag(string key) => Props.TryGetValue(key, out string? value) && value == "1";

        public string? Prop(string key) => Props.TryGetValue(key, out string? value) && value.Length > 0 ? value : null;
    }

    public static MountEntry[] Parse(string output)
    {
        List<Node> roots = [];
        List<Node> open = [];
        Node? current = null;

        foreach (string raw in output.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (Header().Match(line) is { Success: true } header)
            {
                int indent = header.Groups["indent"].Length;
                open.RemoveAll(n => n.Indent >= indent);
                Node? parent = open.Count > 0 ? open[^1] : null;
                current = new Node(header.Groups["kind"].Value, indent, header.Groups["name"].Value, parent);
                if (parent is null)
                    roots.Add(current);
                else
                    parent.Children.Add(current);
                open.Add(current);
            }
            else if (current is not null && Property().Match(line) is { Success: true } prop)
            {
                string value = prop.Groups["value"].Success ? prop.Groups["value"].Value : prop.Groups["quoted"].Value;
                current.Props.TryAdd(prop.Groups["key"].Value, value.Trim());
            }
        }

        List<MountEntry> entries = [];
        foreach (Node node in roots)
            Collect(node, entries);
        return [.. entries];
    }

    private static void Collect(Node node, List<MountEntry> entries)
    {
        switch (node.Kind)
        {
            case "Drive":
                foreach (Node child in node.Children)
                    Collect(child, entries);
                break;
            case "Volume":
                if (FromVolume(node) is { } volume)
                    entries.Add(volume);
                break;
            case "Mount":
                if (FromMount(node, volume: null) is { } mount)
                    entries.Add(mount);
                break;
        }
    }

    private static MountEntry? FromVolume(Node volume)
    {
        string? device = volume.Prop("unix-device");
        string? activation = volume.Prop("activation_root");
        Node? mount = volume.Children.FirstOrDefault(c => c.Kind == "Mount");
        if (mount is not null)
            return FromMount(mount, volume);
        if (device is null && activation is null)
            return null;

        return new MountEntry(
            volume.Name,
            KindOf(volume, activation),
            device,
            activation,
            MountUri: null,
            LocalPath: null,
            CanMount: volume.Flag("can_mount"),
            CanUnmount: false,
            CanEject: volume.Flag("can_eject"));
    }

    private static MountEntry? FromMount(Node mount, Node? volume)
    {
        if (mount.Flag("is_shadowed"))
            return null;
        (string name, string? uri) = SplitMountName(mount.Name);
        if (uri is null)
            return null;

        string? activation = volume?.Prop("activation_root");
        MountKind kind = volume is null
            ? (IsPhone(uri) ? MountKind.Phone : MountKind.Network)
            : KindOf(volume, activation ?? uri);

        return new MountEntry(
            name,
            kind,
            volume?.Prop("unix-device"),
            activation,
            uri,
            MountAddress.LocalPathOfFileUri(uri),
            CanMount: false,
            CanUnmount: mount.Flag("can_unmount"),
            CanEject: mount.Flag("can_eject"));
    }

    private static MountKind KindOf(Node volume, string? uri)
    {
        if (uri is not null && IsPhone(uri))
            return MountKind.Phone;
        if (uri is not null && !uri.StartsWith("file://", StringComparison.Ordinal) && volume.Prop("unix-device") is null)
            return MountKind.Network;
        Node? drive = volume.Parent is { Kind: "Drive" } parent ? parent : null;
        bool removable = volume.Flag("can_eject")
            || drive is not null && (drive.Flag("is_removable") || drive.Flag("can_eject"));
        return removable ? MountKind.Removable : MountKind.Disk;
    }

    private static bool IsPhone(string uri) =>
        _phoneSchemes.Any(scheme => uri.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));

    private static (string Name, string? Uri) SplitMountName(string header)
    {
        int arrow = header.LastIndexOf(" -> ", StringComparison.Ordinal);
        return arrow < 0 ? (header, null) : (header[..arrow], header[(arrow + 4)..].Trim());
    }
}

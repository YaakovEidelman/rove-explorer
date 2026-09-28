namespace Rove.Core.Services;

public sealed class LinuxToolInstaller : IToolInstaller
{
    private const string OsRelease = "/etc/os-release";
    private const string FallbackOsRelease = "/usr/lib/os-release";

    private readonly string _osRelease = Read(OsRelease) ?? Read(FallbackOsRelease) ?? "";

    public string[]? CommandFor(MountTool tool) =>
        IsReadOnly() ? null : ToolPackages.Command(_osRelease, tool);

    public string? RunInTerminal(string[] command) => LinuxTerminal.Run(command);

    private bool IsReadOnly() =>
        ToolPackages.IsReadOnlySystem(_osRelease)
        || File.Exists("/run/ostree-booted")
        || File.Exists("/.flatpak-info");

    private static string? Read(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

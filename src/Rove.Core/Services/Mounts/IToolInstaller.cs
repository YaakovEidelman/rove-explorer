namespace Rove.Core.Services;

public interface IToolInstaller
{
    string[]? CommandFor(MountTool tool);

    string? RunInTerminal(string[] command);
}

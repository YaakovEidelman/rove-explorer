using Rove.Core.Protocol;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private void OpenTerminalHere()
    {
        if (RefusedInArchive("Open Terminal Here") || RefusedInTrash("Open Terminal Here"))
            return;

        _ = OpenTerminalAsync(DirectoryListing.CurrentDir);
    }

    private async Task OpenTerminalAsync(string directory)
    {
        CommandResult<string?> result = await Task.Run(() => _core.Actions.OpenTerminal(new(directory)));
        if (!result.IsOk)
            ErrorRaised?.Invoke(result.Message ?? "Could not open a terminal here.");
    }
}

using Rove.Core.Protocol;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private void ShowOpenWith()
    {
        if (RefusedInArchive("Open With") || RefusedInAdminView("Open With") || RefusedInTrash("Open With"))
            return;
        if (HighlightedItem is not { } highlighted || highlighted.Item.IsDirectory)
        {
            InfoRaised?.Invoke("Open With needs a file highlighted.");
            return;
        }
        ShowAppPicker(highlighted.Item, null, all: false);
    }

    private void ShowAppPicker(FolderItem item, string? note, bool all)
    {
        foreach (string id in _registry.CommandIdsStartingWith(CommandDef.OpenWithIdPrefix))
            _registry.Unregister(id);

        AppPickerRequested?.Invoke(LoadAppsAsync(item, note, all));
    }

    private async Task LoadAppsAsync(FolderItem item, string? note, bool all)
    {
        CommandResult<AppEntry[]> result = await Task.Run(
            () => _core.Actions.ListOpenWithApps(new(item.FullPath, all)));
        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(note ?? result.Message ?? $"Could not list apps for {item.Name}.");
            return;
        }

        AppEntry[] apps = result.Data ?? [];
        if (note is not null)
            InfoRaised?.Invoke(note);
        else if (apps.Length == 0)
            InfoRaised?.Invoke(all ? "No apps were found on this system." : $"Nothing is set up to open {item.Name}.");

        for (int i = 0; i < apps.Length; i++)
        {
            AppEntry app = apps[i];
            CommandDef def = new(CommandDef.OpenWithIdPrefix + app.Id, $"Open with {app.Name}", CommandKind.User, i);
            _registry.Register(def, () => _ = OpenWithAsync(item, app));
        }

        if (!all)
        {
            CommandDef others = new(CommandDef.OpenWithOthersId, "Other apps…", CommandKind.User, apps.Length);
            _registry.Register(others, () => ShowAppPicker(item, null, all: true));
        }
    }

    private async Task OpenWithAsync(FolderItem item, AppEntry app)
    {
        CommandResult<string?> result = await _core.Actions.LaunchFileWithAsync(new(item.FullPath, app.DesktopFile));
        if (!result.IsOk)
            ErrorRaised?.Invoke(result.Message ?? $"Could not open {item.Name} with {app.Name}.");
    }
}

using Rove.Core.Protocol;
using Rove.UI.Services;
using System.Threading.Tasks;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private async Task OfferAppPickerAsync(FolderItem item, string refusal)
    {
        CommandResult<AppEntry[]> result = await Task.Run(() => _core.Actions.ListOpenWithApps(new()));
        if (!result.IsOk || result.Data is not { Length: > 0 } apps)
        {
            ErrorRaised?.Invoke(refusal);
            return;
        }

        foreach (string id in _registry.CommandIdsStartingWith(CommandDef.OpenWithIdPrefix))
            _registry.Unregister(id);

        foreach (AppEntry app in apps)
        {
            CommandDef def = new(CommandDef.OpenWithIdPrefix + app.DesktopFile, $"Open with {app.Name}", CommandKind.User);
            _registry.Register(def, () => _ = OpenWithAsync(item, app));
        }

        InfoRaised?.Invoke($"{refusal} Pick an app to open it with.");
        AppPickerRequested?.Invoke();
    }

    private async Task OpenWithAsync(FolderItem item, AppEntry app)
    {
        CommandResult<string?> result = await _core.Actions.LaunchFileWithAsync(new(item.FullPath, app.DesktopFile));
        if (!result.IsOk)
            ErrorRaised?.Invoke(result.Message ?? $"Could not open {item.Name} with {app.Name}.");
    }
}

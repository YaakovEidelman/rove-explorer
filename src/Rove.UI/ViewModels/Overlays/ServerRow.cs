using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public sealed record ServerRow(bool IsAddNew, SavedServer? Server, MountEntry? Connection, string Title, string Detail)
{
    public bool IsConnected => Connection is not null;

    public bool HasSavedPassword => Server is { SavePassword: true };
}

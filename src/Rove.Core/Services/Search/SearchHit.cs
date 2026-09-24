using Rove.Core.Protocol;

namespace Rove.Core.Services;

public sealed record SearchHit(FolderItem Item, int Score);

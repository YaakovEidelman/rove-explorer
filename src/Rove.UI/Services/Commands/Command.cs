namespace Rove.UI.Services;

public readonly record struct Command(CommandDef Def, Action Method, Func<bool>? CanRun = null)
{
    public bool IsRunnable => CanRun?.Invoke() ?? true;
}

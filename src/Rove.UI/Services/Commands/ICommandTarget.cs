namespace Rove.UI.Services;

public interface ICommandTarget
{
    void Register(CommandDef def, Action method, Func<bool>? canRun = null);

    void Unregister(string commandId);

    IEnumerable<string> CommandIdsStartingWith(string prefix);
}

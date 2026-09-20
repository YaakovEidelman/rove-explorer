namespace Rove.UI.Services;

public readonly record struct Command(CommandDef Def, Action Method);

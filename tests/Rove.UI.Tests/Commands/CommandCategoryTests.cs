using Rove.UI.Services;
using System.Reflection;
using Xunit;

namespace Rove.UI.Tests;

public class CommandCategoryTests
{
    private static IEnumerable<CommandDef> UserCommandDefs() =>
        typeof(CommandDef)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(CommandDef))
            .Select(f => (CommandDef)f.GetValue(null)!)
            .Where(def => def.CommandKind == CommandKind.User);

    [Fact]
    public void EveryUserCommandHasACategory()
    {
        CommandDef[] uncategorized = [.. UserCommandDefs().Where(def => def.Category == CommandCategory.None)];

        Assert.Equal([], uncategorized.Select(def => def.Id));
    }
}

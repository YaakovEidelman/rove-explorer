using Avalonia.Headless;

namespace Rove.UI.Tests;

public abstract class HeadlessTest
{
    private static readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTest).Assembly);

    protected static Task OnUiThread(Action body) => _session.Dispatch(body, CancellationToken.None);

    protected static Task<T> OnUiThread<T>(Func<Task<T>> body) => _session.Dispatch(body, CancellationToken.None);
}

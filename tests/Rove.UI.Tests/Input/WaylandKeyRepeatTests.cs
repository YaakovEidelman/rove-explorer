using Avalonia.Controls;
using Rove.UI.Services;
using System.Reflection;
using Xunit;

namespace Rove.UI.Tests;

public class WaylandKeyRepeatTests : HeadlessTest
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void AvaloniaStillHasTheWaylandFieldsTheRateReaderReaches()
    {
        Type impl = Type.GetType("Avalonia.Wayland.WindowBaseImpl, Avalonia.Wayland", throwOnError: true)!;
        Type sink = impl.GetNestedType("Sink", BindingFlags.NonPublic)!;

        Assert.NotNull(impl.GetProperty("CurrentSink", Instance)?.GetGetMethod(nonPublic: true));
        Assert.Equal(typeof(int), sink.GetField("_keyRepeatRate", Instance)?.FieldType);
        Assert.Equal(typeof(int), sink.GetField("_keyRepeatDelay", Instance)?.FieldType);
    }

    [Fact]
    public Task AWindowOffWaylandHasNoRate() => OnUiThread(() =>
    {
        Window window = new();
        window.Show();

        Assert.Null(WaylandKeyRepeat.RateOf(window));

        window.Close();
    });

    [Fact]
    public void RepeatsPerSecondBecomeAnInterval()
    {
        Assert.Equal(new KeyRepeatRate(250, 25), KeyRepeatRate.FromPerSecond(40, 250));
        Assert.Null(KeyRepeatRate.FromPerSecond(0, 250));
    }
}

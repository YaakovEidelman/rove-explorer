using Avalonia.Headless;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: AvaloniaTestApplication(typeof(Rove.UI.Tests.TestAppBuilder))]

using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Cartex.UiTests.TestAppBuilder))]

namespace Cartex.UiTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

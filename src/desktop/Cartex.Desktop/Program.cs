using Avalonia;

namespace Cartex.Desktop;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        NativeSplash.Show();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

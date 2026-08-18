using Avalonia;

namespace Cartex.Desktop;

class Program
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

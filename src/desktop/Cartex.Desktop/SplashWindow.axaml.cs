using System.Reflection;
using Avalonia.Controls;

namespace Cartex.Desktop;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "" : $"v{version.ToString(3)}";
    }
}

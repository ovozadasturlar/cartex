using Avalonia;
using Avalonia.Markup.Xaml;

namespace Cartex.UiTests;

/// A minimal application shell: the real one wires DI, hubs and a login flow, none of which a
/// render check needs. Only the theme and the app styles matter, because that is what the views
/// resolve their brushes and control classes from.
public class TestApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}

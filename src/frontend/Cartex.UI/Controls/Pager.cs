using Avalonia;
using Avalonia.Controls;

namespace Cartex.UI.Controls;

public class Pager : ContentControl
{
    public static readonly StyledProperty<object?> StateProperty =
        AvaloniaProperty.Register<Pager, object?>(nameof(State));

    public object? State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
}

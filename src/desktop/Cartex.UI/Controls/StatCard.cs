using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Material.Icons;

namespace Cartex.UI.Controls;

public class StatCard : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Value));

    public static readonly StyledProperty<MaterialIconKind> IconProperty =
        AvaloniaProperty.Register<StatCard, MaterialIconKind>(nameof(Icon));

    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<StatCard, IBrush?>(nameof(Accent));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public MaterialIconKind Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
}

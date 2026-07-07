using Avalonia;
using Avalonia.Controls.Primitives;

namespace Cartex.UI.Controls;

public class FieldLabel : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FieldLabel, string?>(nameof(Text));

    public static readonly StyledProperty<bool> RequiredProperty =
        AvaloniaProperty.Register<FieldLabel, bool>(nameof(Required));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool Required { get => GetValue(RequiredProperty); set => SetValue(RequiredProperty, value); }
}

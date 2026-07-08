using Avalonia;
using Avalonia.Controls;
using Material.Icons;

namespace Cartex.UI.Controls;

public class EmptyState : ContentControl
{
    public static readonly StyledProperty<MaterialIconKind> IconProperty =
        AvaloniaProperty.Register<EmptyState, MaterialIconKind>(nameof(Icon), MaterialIconKind.InboxOutline);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Message));

    public MaterialIconKind Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
}

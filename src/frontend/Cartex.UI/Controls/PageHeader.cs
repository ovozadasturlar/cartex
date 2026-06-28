using Avalonia;
using Avalonia.Controls;

namespace Cartex.UI.Controls;

public class PageHeader : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Subtitle));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
}

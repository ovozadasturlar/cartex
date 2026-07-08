using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Cartex.UI.Controls;

public class FilterBar : ContentControl
{
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<FilterBar, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<FilterBar, string?>(nameof(Watermark));

    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public string? Watermark { get => GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }
}

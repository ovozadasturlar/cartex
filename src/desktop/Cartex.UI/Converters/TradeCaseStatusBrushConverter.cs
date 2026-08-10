using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Cartex.UI.Converters;

public sealed class TradeCaseStatusBrushConverter : IValueConverter
{
    public static readonly TradeCaseStatusBrushConverter Instance = new();

    private static readonly SolidColorBrush Open = new(Color.Parse("#0EA5E9"));
    private static readonly SolidColorBrush Pending = new(Color.Parse("#F59E0B"));
    private static readonly SolidColorBrush Settled = new(Color.Parse("#059669"));
    private static readonly SolidColorBrush Cancelled = new(Color.Parse("#64748B"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value?.ToString() switch
    {
        "Open" => Open,
        "SettlementPending" => Pending,
        "Settled" => Settled,
        _ => Cancelled
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

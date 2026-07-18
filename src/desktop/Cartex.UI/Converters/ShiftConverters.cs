using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Cartex.UI.Services;

namespace Cartex.UI.Converters;

public sealed class IsZeroConverter : IValueConverter
{
    public static readonly IsZeroConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var zero = value switch
        {
            decimal d => d == 0,
            int i => i == 0,
            _ => false
        };
        return parameter as string == "not" ? !zero : zero;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CashDiffConverter : IValueConverter
{
    public static readonly CashDiffConverter Instance = new();

    private static readonly SolidColorBrush Shortage = new(Color.Parse("#DC2626"));
    private static readonly SolidColorBrush Surplus = new(Color.Parse("#F59E0B"));
    private static readonly SolidColorBrush Balanced = new(Color.Parse("#059669"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var diff = value as decimal? ?? 0m;
        var l = LocalizationManager.Instance;
        return (parameter as string) switch
        {
            "brush" => diff < 0 ? Shortage : diff > 0 ? Surplus : Balanced,
            "label" => l[diff < 0 ? "shortage" : diff > 0 ? "surplus" : "equal"],
            var format => diff < 0
                ? $"{l["shortage"]}: -{Math.Abs(diff).ToString(format ?? "N0", culture)}"
                : diff > 0
                    ? $"{l["surplus"]}: +{diff.ToString(format ?? "N0", culture)}"
                    : $"✓ {l["equal"]}"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

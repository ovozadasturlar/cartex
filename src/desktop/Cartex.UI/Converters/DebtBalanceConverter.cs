using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Cartex.UI.Services;

namespace Cartex.UI.Converters;

public sealed class DebtBalanceConverter : IValueConverter
{
    public static readonly DebtBalanceConverter Instance = new();

    private static readonly SolidColorBrush Debt = new(Color.Parse("#DC2626"));
    private static readonly SolidColorBrush Credit = new(Color.Parse("#059669"));
    private static readonly SolidColorBrush Muted = new(Color.Parse("#94A3B8"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var balance = value as decimal? ?? 0m;
        return (parameter as string) switch
        {
            "kind" => LocalizationManager.Instance[balance > 0 ? "opening_kind_debt" : "opening_kind_credit"],
            "supkind" => LocalizationManager.Instance[balance > 0 ? "debt" : "advance"],
            "haskind" => balance != 0,
            "brush" => balance == 0 ? Muted : balance > 0 ? Debt : Credit,
            _ => Math.Abs(balance).ToString("N0", culture)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

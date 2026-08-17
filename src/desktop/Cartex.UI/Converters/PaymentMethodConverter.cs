using System.Globalization;
using Avalonia.Data.Converters;
using Cartex.UI.Services;

namespace Cartex.UI.Converters;

public sealed class PaymentMethodConverter : IValueConverter
{
    public static readonly PaymentMethodConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString()?.ToLowerInvariant() switch
        {
            "cash" => LocalizationManager.Instance["cash"],
            "card" => LocalizationManager.Instance["card"],
            "bonus" => LocalizationManager.Instance["bonus"],
            "transfer" => LocalizationManager.Instance["pay_transfer"],
            "bank" => LocalizationManager.Instance["pay_bank"],
            _ => value
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

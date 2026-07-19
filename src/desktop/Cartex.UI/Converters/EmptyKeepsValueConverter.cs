using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Cartex.UI.Converters;

public sealed class EmptyKeepsValueConverter : IValueConverter
{
    public static readonly EmptyKeepsValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? null : System.Convert.ToDecimal(value, culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return BindingOperations.DoNothing;

        var target = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try { return System.Convert.ChangeType(value, target, culture); }
        catch { return BindingOperations.DoNothing; }
    }
}

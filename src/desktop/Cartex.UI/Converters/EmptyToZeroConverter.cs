using System.Globalization;
using Avalonia.Data.Converters;

namespace Cartex.UI.Converters;

public sealed class EmptyToZeroConverter : IValueConverter
{
    public static readonly EmptyToZeroConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? null : System.Convert.ToDecimal(value, culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        if (value is null)
            return underlying is not null ? null : Activator.CreateInstance(targetType);

        var target = underlying ?? targetType;
        try { return System.Convert.ChangeType(value, target, culture); }
        catch { return Activator.CreateInstance(target); }
    }
}

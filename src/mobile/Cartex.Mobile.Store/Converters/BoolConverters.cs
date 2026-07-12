using System.Globalization;

namespace Cartex.Mobile.Store.Converters;

public class PositiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class DotFillConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var filled = value is int i && int.TryParse(parameter?.ToString(), out var n) && i >= n;
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        return filled
            ? (Color)(Application.Current!.Resources[dark ? "PrimaryDark" : "Primary"])
            : Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class InvertConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;
}

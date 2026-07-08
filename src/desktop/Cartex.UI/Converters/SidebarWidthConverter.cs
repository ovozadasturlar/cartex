using System.Globalization;
using Avalonia.Data.Converters;

namespace Cartex.UI.Converters;

public sealed class SidebarWidthConverter : IValueConverter
{
    public static readonly SidebarWidthConverter Instance = new();
    public static readonly SidebarLogoSizeConverter LogoSize = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 60.0 : 240.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SidebarLogoSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 28.0 : 36.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

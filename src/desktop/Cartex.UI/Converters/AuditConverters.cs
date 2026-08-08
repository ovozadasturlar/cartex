using System.Globalization;
using Avalonia.Data.Converters;
using Cartex.UI.Services;

namespace Cartex.UI.Converters;

public class AuditActionConverter : IValueConverter
{
    public static readonly AuditActionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string action) return AuditTranslator.TranslateAction(action);
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class AuditTableConverter : IValueConverter
{
    public static readonly AuditTableConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string table) return AuditTranslator.TranslateTable(table);
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

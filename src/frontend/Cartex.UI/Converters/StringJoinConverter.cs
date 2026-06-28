using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Cartex.UI.Converters;

public sealed class StringJoinConverter : IValueConverter
{
    public static readonly StringJoinConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable items and not string
            ? string.Join(", ", items.Cast<object?>().Where(o => o is not null))
            : value ?? string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

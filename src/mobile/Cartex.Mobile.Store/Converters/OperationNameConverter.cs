using System.Globalization;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Converters;

public class OperationNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value?.ToString() is not { Length: > 0 } name) return string.Empty;
        var key = $"op_{name.ToLowerInvariant()}";
        var text = Loc.Instance[key];
        return text == key ? name : text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

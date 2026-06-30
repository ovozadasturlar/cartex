using System.Globalization;
using Avalonia.Data.Converters;
using Cartex.UI.Services;

namespace Cartex.UI.Converters;

public sealed class EnumLocalizationConverter : IValueConverter
{
    public static readonly EnumLocalizationConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        var prefix = parameter as string ?? "op";
        var key = $"{prefix}_{value.ToString()!.ToLowerInvariant()}";
        var text = LocalizationManager.Instance[key];
        return text == $"[{key}]" ? value.ToString()! : text;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

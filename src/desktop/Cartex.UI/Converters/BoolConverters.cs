using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value!;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value!;
}

public sealed class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 1.0 : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 1.0;
}

public sealed class IntPositiveConverter : IValueConverter
{
    public static readonly IntPositiveConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DecimalPositiveConverter : IValueConverter
{
    public static readonly DecimalPositiveConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var positive = value is decimal d && d > 0;
        return parameter is "inverse" ? !positive : positive;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PackQtyConverter : IValueConverter
{
    public static readonly PackQtyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is decimal d && d > 1 ? $"× {d:0.###}" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ImageUrlConverter : IValueConverter
{
    public static readonly ImageUrlConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ImageUrl.Absolute(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ThumbImageUrlConverter : IValueConverter
{
    public static readonly ThumbImageUrlConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ImageUrl.Thumb(ImageUrl.Absolute(value as string));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringNotEmptyConverter : IValueConverter
{
    public static readonly StringNotEmptyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value?.ToString();
        return parameter?.ToString()?.Split(',').Any(p => string.Equals(current, p, StringComparison.Ordinal)) ?? current is null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ClientDisplayConverter : IValueConverter
{
    public static readonly ClientDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrEmpty(s)
            ? LocalizationManager.Instance.Find("client_" + s) ?? s
            : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class LanguageFlagConverter : IValueConverter
{
    public static readonly LanguageFlagConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AppLanguage lang ? LocalizationManager.GetLanguageShortCode(lang) : value!;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class LanguageDisplayConverter : IValueConverter
{
    public static readonly LanguageDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AppLanguage lang ? LocalizationManager.GetLanguageDisplayName(lang) : value!;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToOrientationConverter : IValueConverter
{
    public static readonly BoolToOrientationConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Orientation.Vertical : Orientation.Horizontal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToColumnConverter : IValueConverter
{
    public static readonly BoolToColumnConverter Col0Or2 = new(0, 2);
    public static readonly BoolToColumnConverter Col2Or0 = new(2, 0);
    public static readonly BoolToColumnConverter Row0Or2 = new(0, 2);
    public static readonly BoolToColumnConverter Row2Or0 = new(2, 0);

    private readonly int _falseVal;
    private readonly int _trueVal;

    public BoolToColumnConverter(int falseVal, int trueVal)
    {
        _falseVal = falseVal;
        _trueVal = trueVal;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? _trueVal : _falseVal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

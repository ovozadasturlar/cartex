using System.Globalization;
using Avalonia.Data.Converters;
using Cartex.UI.Services;

namespace Cartex.UI.Converters;

/// SOZ-02a: bo'sh limit "cheklanmagan" degani. Ro'yxatda uni bo'sh katak bilan ko'rsatish
/// cheklanmaganni yuklanmagandan ajratmaydi, shuning uchun so'z bilan yoziladi.
public sealed class CreditLimitConverter : IValueConverter
{
    public static readonly CreditLimitConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is decimal limit ? limit.ToString("N0", culture) : LocalizationManager.Instance["unlimited"];

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

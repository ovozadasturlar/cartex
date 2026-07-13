using System.Globalization;
using Avalonia.Data.Converters;

namespace Cartex.UI.Converters;

/// <summary>
/// NumericUpDown maydoni bo'shatilganda uning qiymati null bo'ladi. Model xossalari esa decimal/int —
/// null ularga yozilmaydi va foydalanuvchiga qizil "InvalidCastException" ko'rinadi. Bu konverter bo'sh
/// maydonni jimgina 0 (yoki xossa turi bo'yicha default) deb qabul qiladi: input bo'sh qolaveradi,
/// serverga esa 0 boradi.
/// </summary>
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

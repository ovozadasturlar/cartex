using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Cartex.UI.Converters;

public sealed class ExpiryBrushConverter : IValueConverter
{
    public static readonly ExpiryBrushConverter Instance = new();

    private static readonly SolidColorBrush Danger = new(Color.Parse("#DC2626"));
    private static readonly SolidColorBrush Warning = new(Color.Parse("#F59E0B"));
    private static readonly SolidColorBrush Muted = new(Color.Parse("#94A3B8"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateOnly date) return Muted;
        var days = date.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        return days <= 0 ? Danger : days <= 7 ? Warning : Muted;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

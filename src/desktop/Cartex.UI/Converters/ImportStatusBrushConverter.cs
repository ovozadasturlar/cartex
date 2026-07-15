using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Cartex.Shared.Models.Products;

namespace Cartex.UI.Converters;

public sealed class ImportStatusBrushConverter : IValueConverter
{
    public static readonly ImportStatusBrushConverter Instance = new();

    private static readonly SolidColorBrush Create = new(Color.Parse("#16A34A"));
    private static readonly SolidColorBrush Existing = new(Color.Parse("#2563EB"));
    private static readonly SolidColorBrush Skip = new(Color.Parse("#DC2626"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ImportRowAction.Existing => Existing,
        ImportRowAction.Skip => Skip,
        _ => Create
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

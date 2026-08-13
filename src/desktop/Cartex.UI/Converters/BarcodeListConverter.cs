using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;
using Cartex.Shared.Models.Products;

namespace Cartex.UI.Converters;

public sealed class BarcodeListConverter : IValueConverter
{
    public static readonly BarcodeListConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable items
            ? string.Join(", ", items.OfType<VariantBarcodeDto>().Select(Format))
            : string.Empty;

    private static string Format(VariantBarcodeDto barcode) =>
        barcode.PackQty > 1 ? $"{barcode.Code}×{barcode.PackQty:0.##}" : barcode.Code;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

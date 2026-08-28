using System.Globalization;
using Cartex.Catalog.Tool.Io;
using Cartex.Catalog.Tool.Normalization;

namespace Cartex.Catalog.Tool.Packaging;

public sealed record PackRow(
    string Barcode,
    string Name,
    string NameCyrillic,
    string SearchFold,
    string? Manufacturer,
    string? Category,
    string? Model,
    string Unit,
    double? PackQuantity,
    string? ImagePath);

public static class CatalogPack
{
    public const string AllShopTypes = "all";

    private static readonly string[] RequiredColumns = ["barcode", "name", "search_fold", "unit"];

    public static IReadOnlyList<PackRow> Read(string path, ShopType? shopType)
    {
        var records = CsvFile.Read(path);
        if (records.Count == 0)
            throw new InvalidDataException($"'{path}' holds no header row.");

        var columns = Columns(path, records[0], shopType);
        var rows = new List<PackRow>(records.Count - 1);

        foreach (var record in records.Skip(1))
        {
            if (record.All(field => field.Length == 0))
                continue;

            var barcode = CsvFile.Value(record, columns, "barcode");
            if (barcode.Length == 0)
                throw new InvalidDataException("a catalog row carries no barcode (GKAT-10).");

            if (!Selected(record, columns, shopType))
                continue;

            var manufacturer = CsvFile.Value(record, columns, "manufacturer");
            var model = CsvFile.Value(record, columns, "model");
            var name = CsvFile.Value(record, columns, "name");

            rows.Add(new PackRow(
                barcode,
                name,
                Transliterator.ToCyrillic(name, manufacturer, model),
                CsvFile.Value(record, columns, "search_fold"),
                Optional(manufacturer),
                Category(CsvFile.Value(record, columns, "category_parent"), CsvFile.Value(record, columns, "category_child")),
                Optional(model),
                CsvFile.Value(record, columns, "unit"),
                Quantity(CsvFile.Value(record, columns, "pack_qty")),
                ImagePath(barcode, CsvFile.Value(record, columns, "image_url"))));
        }

        return [.. rows.OrderBy(row => row.Barcode, StringComparer.Ordinal)];
    }

    private static Dictionary<string, int> Columns(string path, IReadOnlyList<string> header, ShopType? shopType)
    {
        var columns = CsvFile.Columns(path, header, RequiredColumns);
        if (shopType is not null && !columns.ContainsKey(SegmentMap.Column))
            throw new InvalidDataException(
                $"--shop-type needs a '{SegmentMap.Column}' column; segment membership belongs to the master catalog (GKAT-50, GKAT-51).");

        return columns;
    }

    private static bool Selected(string[] record, Dictionary<string, int> columns, ShopType? shopType) =>
        shopType is null ||
        SegmentMap.Split(CsvFile.Value(record, columns, SegmentMap.Column)).Any(shopType.Segments.Contains);

    private static string? Category(string parent, string child) => (parent.Length, child.Length) switch
    {
        (0, 0) => null,
        (_, 0) => parent,
        (0, _) => child,
        _ => $"{parent} / {child}"
    };

    private static string? ImagePath(string barcode, string image) =>
        image.Length == 0 ? null : $"img/{barcode}.webp";

    private static double? Quantity(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) ? quantity : null;

    private static string? Optional(string value) => value.Length == 0 ? null : value;
}

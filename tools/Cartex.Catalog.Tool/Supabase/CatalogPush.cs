using System.Globalization;
using Cartex.Catalog.Tool.Io;
using Cartex.Catalog.Tool.Normalization;

namespace Cartex.Catalog.Tool.Supabase;

public sealed record ManufacturerRow(long Id, string Name);

public sealed record ManufacturerName(string Name);

public sealed record CategoryRow(long Id, long? ParentId, string Name);

public sealed record ProductRow(
    string Barcode,
    string Name,
    string SearchFold,
    long? ManufacturerId,
    long? CategoryId,
    string? Model,
    string Unit,
    decimal? PackQty,
    string? ImagePath,
    string[] Segments,
    string Status,
    string? Source);

public sealed record PushReport(
    int Products,
    int Manufacturers,
    int NewManufacturers,
    int CategoriesResolved,
    int CategoriesMissing,
    int Created,
    int Changed,
    int Unchanged,
    int OnlyInMaster);

public static class CatalogPush
{
    private const string ProductColumns = "barcode,name,search_fold,manufacturer_id,category_id,model,unit,pack_qty,image_path,segments,status,source";
    private const string Published = "published";
    private const string DefaultUnit = "dona";
    private const int BatchSize = 500;
    private const long PendingId = 0;

    private static readonly string[] RequiredColumns = ["barcode", "name", "search_fold", "unit"];

    public static async Task<PushReport> Run(MasterCatalog catalog, string path, bool dryRun, Action<string> progress)
    {
        var records = CsvFile.Read(path);
        if (records.Count == 0)
            throw new InvalidDataException($"'{path}' holds no header row.");

        var columns = CsvFile.Columns(path, records[0], RequiredColumns);
        var input = records.Skip(1).Where(record => record.Any(field => field.Length > 0)).ToList();

        var brands = input.Select(record => CsvFile.Value(record, columns, "manufacturer"))
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        progress("reading manufacturers");
        var (manufacturers, added) = await Manufacturers(catalog, brands, dryRun, progress);

        progress("reading categories");
        var categories = await Categories(catalog);

        var barcodes = new HashSet<string>(StringComparer.Ordinal);
        var wanted = new List<ProductRow>(input.Count);
        var missing = 0;

        foreach (var record in input)
        {
            string Value(string column) => CsvFile.Value(record, columns, column);

            var barcode = Value("barcode");
            if (barcode.Length == 0)
                throw new InvalidDataException("a catalog row carries no barcode (GKAT-10).");
            if (!barcodes.Add(barcode))
                throw new InvalidDataException($"barcode '{barcode}' appears twice in '{path}'; the catalog key is unique (GKAT-13).");

            var parent = Value("category_parent");
            var manufacturer = Value("manufacturer");
            long? category = categories.TryGetValue(Key(parent, Value("category_child")), out var found) ? found : null;
            if (category is null)
                missing++;

            wanted.Add(new ProductRow(
                barcode,
                Value("name"),
                Value("search_fold"),
                manufacturer.Length == 0 ? null : manufacturers[manufacturer],
                category,
                Optional(Value("model")),
                Unit(Value("unit")),
                Quantity(Value("pack_qty")),
                Value("image_url").Length == 0 ? null : $"img/{barcode}.webp",
                SegmentMap.Resolve(Value(SegmentMap.Column), parent),
                Published,
                Optional(Value("source"))));
        }

        progress("reading products");
        var master = (await catalog.Read<ProductRow>("catalog_products", ProductColumns))
            .ToDictionary(row => row.Barcode, StringComparer.Ordinal);

        var changes = wanted.Where(row => !master.TryGetValue(row.Barcode, out var current) || !Same(current, row)).ToList();
        var created = changes.Count(row => !master.ContainsKey(row.Barcode));

        if (!dryRun && changes.Count > 0)
            await WriteProducts(catalog, changes, progress);

        return new PushReport(
            wanted.Count,
            brands.Count,
            added,
            wanted.Count - missing,
            missing,
            created,
            changes.Count - created,
            wanted.Count - changes.Count,
            master.Count - master.Keys.Count(barcodes.Contains));
    }

    private static async Task<(Dictionary<string, long> Map, int Added)> Manufacturers(
        MasterCatalog catalog, IReadOnlyList<string> names, bool dryRun, Action<string> progress)
    {
        var map = await ManufacturerMap(catalog);
        var added = names.Where(name => !map.ContainsKey(name)).ToList();
        if (added.Count == 0)
            return (map, 0);

        if (dryRun)
        {
            foreach (var name in added)
                map[name] = PendingId;

            return (map, added.Count);
        }

        progress($"writing manufacturers 0/{added.Count}");
        await catalog.Write("catalog_manufacturers", [.. added.Select(name => new ManufacturerName(name))], null);
        progress($"writing manufacturers {added.Count}/{added.Count}");
        return (await ManufacturerMap(catalog), added.Count);
    }

    private static async Task<Dictionary<string, long>> ManufacturerMap(MasterCatalog catalog) =>
        (await catalog.Read<ManufacturerRow>("catalog_manufacturers", "id,name"))
        .ToDictionary(row => row.Name, row => row.Id, StringComparer.OrdinalIgnoreCase);

    private static async Task<Dictionary<string, long>> Categories(MasterCatalog catalog)
    {
        var rows = await catalog.Read<CategoryRow>("catalog_categories", "id,parent_id,name");
        var names = rows.ToDictionary(row => row.Id, row => row.Name);
        return rows.ToDictionary(
            row => row.ParentId is null ? Key(row.Name, string.Empty) : Key(names[row.ParentId.Value], row.Name),
            row => row.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task WriteProducts(MasterCatalog catalog, IReadOnlyList<ProductRow> rows, Action<string> progress)
    {
        for (var start = 0; start < rows.Count; start += BatchSize)
        {
            var batch = rows.Skip(start).Take(BatchSize).ToList();
            await catalog.Write("catalog_products", batch, "barcode");
            progress($"writing products {start + batch.Count}/{rows.Count}");
        }
    }

    private static bool Same(ProductRow current, ProductRow wanted) =>
        current.Name == wanted.Name &&
        current.SearchFold == wanted.SearchFold &&
        current.ManufacturerId == wanted.ManufacturerId &&
        current.CategoryId == wanted.CategoryId &&
        current.Model == wanted.Model &&
        current.Unit == wanted.Unit &&
        current.PackQty == wanted.PackQty &&
        current.ImagePath == wanted.ImagePath &&
        current.Status == wanted.Status &&
        current.Source == wanted.Source &&
        current.Segments.SequenceEqual(wanted.Segments, StringComparer.Ordinal);

    private static string Key(string parent, string child) => $"{parent}\n{child}";

    private static string Unit(string value) => value.Length == 0 ? DefaultUnit : value;

    private static string? Optional(string value) => value.Length == 0 ? null : value;

    private static decimal? Quantity(string value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) ? quantity : null;
}

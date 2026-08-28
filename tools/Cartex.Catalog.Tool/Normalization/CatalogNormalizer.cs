using System.Globalization;

namespace Cartex.Catalog.Tool.Normalization;

public sealed record CatalogProduct(
    string Barcode,
    string Name,
    string SearchFold,
    string Manufacturer,
    string CategoryParent,
    string CategoryChild,
    string Model,
    string Unit,
    string PackQuantity,
    string ImageUrl,
    string Source,
    string Segments);

public sealed record CatalogIssue(string Source, int RowNumber, string Barcode, string Name, string Code, string Detail);

public sealed record CatalogReport(
    int RowsRead,
    IReadOnlyList<CatalogProduct> Products,
    IReadOnlyList<CatalogIssue> Issues,
    IReadOnlyList<string> Manufacturers,
    IReadOnlyList<ManufacturerConflict> ManufacturerConflicts);

public static class CatalogNormalizer
{
    public const string DuplicateCode = "GKAT-80";

    public static readonly string[] ProductHeader =
    [
        "barcode", "name", "search_fold", "manufacturer", "category_parent",
        "category_child", "model", "unit", "pack_qty", "image_url", "source", "segments"
    ];

    public static readonly string[] IssueHeader = ["source", "row_number", "barcode", "name", "issue", "detail"];

    public static CatalogReport Normalize(IReadOnlyList<SourceRow> rows, TypeVocabulary types)
    {
        var manufacturers = ManufacturerIndex.Build(rows.Select(row => row.Manufacturer));
        var products = new List<CatalogProduct>(rows.Count);
        var issues = new List<CatalogIssue>();
        var accepted = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var barcode = BarcodeNormalizer.Classify(row.Barcode);
            if (barcode.Value is null)
            {
                issues.Add(new CatalogIssue(row.Source, row.RowNumber, row.Barcode, row.Name, barcode.Issue!, barcode.Detail!));
                continue;
            }

            var code = barcode.Value;
            if (accepted.TryGetValue(code, out var owner))
            {
                issues.Add(new CatalogIssue(row.Source, row.RowNumber, code, row.Name,
                    DuplicateCode, $"barcode was already taken from the '{owner}' source, which wins"));
                continue;
            }

            accepted.Add(code, row.Source);

            var text = NameNormalizer.Clean(row.Name);
            var manufacturer = manufacturers.Canonical(row.Manufacturer);
            if (manufacturer.Length == 0)
            {
                manufacturer = manufacturers.Recover(text);
                if (manufacturer.Length > 0)
                    issues.Add(new CatalogIssue(row.Source, row.RowNumber, code, row.Name,
                        ManufacturerIndex.RecoveredCode, $"manufacturer '{manufacturer}' was read from the name"));
            }

            var normalized = NameNormalizer.Normalize(text, manufacturer, row.Category.Leaf, types);
            var name = NameNormalizer.Shorten(normalized.Value);

            if (normalized.ReviewReason is not null)
                issues.Add(new CatalogIssue(row.Source, row.RowNumber, code, row.Name, NameNormalizer.ReviewCode, normalized.ReviewReason));

            if (name.Length > NameNormalizer.MaxLength)
                issues.Add(new CatalogIssue(row.Source, row.RowNumber, code, row.Name, NameNormalizer.LengthCode,
                    $"name is {name.Length} characters and has no parameters to trim"));
            else if (!string.Equals(name, normalized.Value, StringComparison.Ordinal))
                issues.Add(new CatalogIssue(row.Source, row.RowNumber, code, row.Name, NameNormalizer.LengthCode,
                    "parameters trimmed to fit 80 characters"));

            if (row.Category.Issue is not null)
                issues.Add(new CatalogIssue(row.Source, row.RowNumber, code, row.Name, row.Category.Issue, row.Category.Detail!));

            if (row.Image.Issue is not null)
                issues.Add(new CatalogIssue(row.Source, row.RowNumber, code, row.Name, row.Image.Issue, row.Image.Detail!));

            products.Add(new CatalogProduct(
                code,
                name,
                SearchFold.Build(name),
                manufacturer,
                row.Category.Parent,
                row.Category.Child,
                row.Model,
                row.Unit,
                row.PackQuantity,
                row.Image.Value,
                row.Source,
                SegmentMap.Join(row.Category.Parent)));
        }

        var ordered = products
            .OrderBy(product => product.Name, StringComparer.Ordinal)
            .ThenBy(product => product.Barcode, StringComparer.Ordinal)
            .ToList();

        return new CatalogReport(rows.Count, ordered, issues, manufacturers.Names, manufacturers.Conflicts);
    }

    public static IReadOnlyList<string> ToFields(CatalogProduct product) =>
    [
        product.Barcode, product.Name, product.SearchFold, product.Manufacturer, product.CategoryParent,
        product.CategoryChild, product.Model, product.Unit, product.PackQuantity, product.ImageUrl, product.Source,
        product.Segments
    ];

    public static IReadOnlyList<string> ToFields(CatalogIssue issue) =>
    [
        issue.Source,
        issue.RowNumber.ToString(CultureInfo.InvariantCulture),
        issue.Barcode, issue.Name, issue.Code, issue.Detail
    ];
}

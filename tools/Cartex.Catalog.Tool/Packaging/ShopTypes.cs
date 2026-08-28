using Cartex.Catalog.Tool.Io;
using Cartex.Catalog.Tool.Normalization;

namespace Cartex.Catalog.Tool.Packaging;

public sealed record ShopType(string Code, string Name, IReadOnlySet<string> Segments);

public static class ShopTypes
{
    public const string DefaultPath = "tools/Cartex.Catalog.Tool/shop-types.csv";

    private const string CodeColumn = "code";
    private const string NameColumn = "nom";
    private const string SegmentsColumn = "segmentlar";

    private static readonly string[] Required = [CodeColumn, NameColumn, SegmentsColumn];

    public static ShopType Load(string path, string code)
    {
        var records = CsvFile.Read(path);
        if (records.Count == 0)
            throw new InvalidDataException($"'{path}' holds no header row.");

        var columns = CsvFile.Columns(path, records[0], Required);
        var types = new List<ShopType>();
        foreach (var record in records.Skip(1))
        {
            var entry = CsvFile.Value(record, columns, CodeColumn);
            var segments = SegmentMap.Split(CsvFile.Value(record, columns, SegmentsColumn));
            if (entry.Length > 0 && segments.Length > 0)
                types.Add(new ShopType(entry, CsvFile.Value(record, columns, NameColumn),
                    segments.ToHashSet(StringComparer.OrdinalIgnoreCase)));
        }

        return types.FirstOrDefault(type => type.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(types.Count == 0
                ? $"'{path}' declares no shop type, so no pack can be sliced (GKAT-53)."
                : $"'{code}' is not a shop type in '{path}'; it declares {string.Join(", ", types.Select(type => type.Code))}.");
    }
}

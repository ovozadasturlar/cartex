using Cartex.Catalog.Tool.Normalization;

namespace Cartex.Catalog.Tool.Io;

public static class ServerCatalogReader
{
    public const string Source = "server";

    private const string DefaultUnit = "dona";
    private const string BarcodeColumn = "barcode";
    private const string NameColumn = "name";
    private const string ManufacturerColumn = "manufacturer";
    private const string ParentColumn = "category_parent";
    private const string ChildColumn = "category_child";
    private const string ImageColumn = "image_key";

    private static readonly string[] RequiredColumns = [BarcodeColumn, NameColumn];

    public static IReadOnlyList<SourceRow> Read(string path)
    {
        var records = CsvFile.Read(path);
        if (records.Count == 0)
            throw new InvalidDataException($"'{path}' holds no header row.");

        var columns = CsvFile.Columns(path, records[0], RequiredColumns);
        var rows = new List<SourceRow>(records.Count - 1);

        for (var line = 1; line < records.Count; line++)
        {
            var record = records[line];
            if (record.All(field => field.Length == 0))
                continue;

            string Value(string column) => CsvFile.Value(record, columns, column);

            rows.Add(new SourceRow(
                Source,
                line + 1,
                Value(BarcodeColumn),
                Value(NameColumn),
                Value(ManufacturerColumn),
                CategoryPath.Of(Value(ParentColumn), Value(ChildColumn)),
                string.Empty,
                DefaultUnit,
                string.Empty,
                ImageReference.FromKey(Value(ImageColumn))));
        }

        return rows;
    }
}

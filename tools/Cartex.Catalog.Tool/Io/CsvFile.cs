using System.Text;

namespace Cartex.Catalog.Tool.Io;

public static class CsvFile
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly char[] Quotable = [',', '"', '\r', '\n'];

    public static void Write(string path, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        using var writer = new StreamWriter(path, false, Utf8WithBom) { NewLine = "\r\n" };
        writer.WriteLine(Line(header));
        foreach (var row in rows)
            writer.WriteLine(Line(row));
    }

    public static IReadOnlyList<string[]> Read(string path)
    {
        var text = File.ReadAllText(path);
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var symbol = text[i];
            if (quoted)
            {
                if (symbol != '"')
                {
                    field.Append(symbol);
                }
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (symbol)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add([.. fields]);
                    fields.Clear();
                    break;
                default:
                    field.Append(symbol);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add([.. fields]);
        }

        return records;
    }

    public static Dictionary<string, int> Columns(string path, IReadOnlyList<string> header, IReadOnlyList<string> required)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
            columns[header[i].Trim()] = i;

        var missing = required.Where(column => !columns.ContainsKey(column)).ToList();
        return missing.Count == 0
            ? columns
            : throw new InvalidDataException($"'{path}' is missing required column(s): {string.Join(", ", missing)}.");
    }

    public static string Value(IReadOnlyList<string> record, IReadOnlyDictionary<string, int> columns, string column) =>
        columns.TryGetValue(column, out var index) && index < record.Count ? record[index].Trim() : string.Empty;

    public static string Line(IReadOnlyList<string> fields) => string.Join(',', fields.Select(Field));

    private static string Field(string value) => value.AsSpan().IndexOfAny(Quotable) >= 0
        ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
        : value;
}

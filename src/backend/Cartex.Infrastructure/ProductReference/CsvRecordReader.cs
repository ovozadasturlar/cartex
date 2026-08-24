namespace Cartex.Infrastructure.ProductReference;

public static class CsvRecordReader
{
    public static IReadOnlyList<IReadOnlyList<string>> Parse(TextReader reader, int maxRecords = int.MaxValue)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        var started = false;

        while (reader.Read() is var value && value >= 0)
        {
            started = true;
            var character = (char)value;
            if (quoted)
            {
                if (character == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }
                continue;
            }

            if (character == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (character == ',')
            {
                AddField(row, field, rows.Count == 0 && row.Count == 0);
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && reader.Peek() == '\n') reader.Read();
                AddField(row, field, rows.Count == 0 && row.Count == 0);
                rows.Add(row);
                if (rows.Count > maxRecords)
                    throw new InvalidDataException($"CSV qatorlari {maxRecords} tadan oshmasligi kerak.");
                row = [];
            }
            else
            {
                field.Append(character);
            }
        }

        if (quoted) throw new InvalidDataException("CSV ichida yopilmagan qo'shtirnoq bor.");
        if (started && (field.Length > 0 || row.Count > 0))
        {
            AddField(row, field, rows.Count == 0 && row.Count == 0);
            rows.Add(row);
        }
        return rows;
    }

    private static void AddField(List<string> row, System.Text.StringBuilder field, bool first)
    {
        var value = field.ToString();
        if (first) value = value.TrimStart('\uFEFF');
        row.Add(value);
        field.Clear();
    }
}

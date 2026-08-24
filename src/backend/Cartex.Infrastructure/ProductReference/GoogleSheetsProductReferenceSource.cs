using System.Globalization;
using System.Net;
using System.Text;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common.Exceptions;

namespace Cartex.Infrastructure.ProductReference;

public sealed class GoogleSheetsProductReferenceSource(IHttpClientFactory clients) : IProductReferenceSource
{
    private const int MaxBytes = 20 * 1024 * 1024;
    private const int MaxRows = 100_000;

    public async Task<IReadOnlyList<ProductReferenceRow>> FetchAsync(ProductReferenceSourceConfig config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.SpreadsheetId)
            || config.SpreadsheetId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            throw new BusinessRuleException("Google Sheets hujjat ID si noto'g'ri.", "product_reference_sheet_id_invalid");
        if (string.IsNullOrWhiteSpace(config.SheetName))
            throw new BusinessRuleException("Google Sheets varaq nomi kiritilmagan.", "product_reference_sheet_name_missing");

        var url = $"https://docs.google.com/spreadsheets/d/{config.SpreadsheetId}/gviz/tq?tqx=out:csv&sheet={Uri.EscapeDataString(config.SheetName)}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await clients.CreateClient().GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new BusinessRuleException("Google Sheets varag'i ochiq emas yoki hujjat ID si noto'g'ri.", "product_reference_sheet_unavailable");
        if (!response.IsSuccessStatusCode)
            throw new BusinessRuleException($"Google Sheets {((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)} xato qaytardi.", "product_reference_sheet_error");
        if (response.Content.Headers.ContentLength is > MaxBytes)
            throw new BusinessRuleException("Google Sheets javobi 20 MB chegaradan oshdi.", "product_reference_source_too_large");

        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
        await using var memory = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, timeout.Token);
            if (read == 0) break;
            total += read;
            if (total > MaxBytes)
                throw new BusinessRuleException("Google Sheets javobi 20 MB chegaradan oshdi.", "product_reference_source_too_large");
            await memory.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }
        memory.Position = 0;
        using var reader = new StreamReader(memory, Encoding.UTF8, true);
        IReadOnlyList<IReadOnlyList<string>> records;
        try
        {
            records = CsvRecordReader.Parse(reader, MaxRows + 1);
        }
        catch (InvalidDataException ex)
        {
            throw new BusinessRuleException(ex.Message, "product_reference_csv_invalid");
        }
        if (records.Count == 0) return [];
        if (records.Count - 1 > MaxRows)
            throw new BusinessRuleException("Google Sheets varag'ida 100 000 tadan ko'p qator bor.", "product_reference_source_too_many_rows");

        var headers = records[0]
            .Select((value, index) => (Name: value.Trim(), Index: index))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);
        var barcode = Required(headers, config.BarcodeColumn);
        var name = Required(headers, config.NameColumn);
        var unit = Mapped(headers, config.UnitColumn);
        var category = Mapped(headers, config.CategoryColumn);
        var manufacturer = Mapped(headers, config.ManufacturerColumn);
        var packQty = Mapped(headers, config.PackQtyColumn);
        var price = Mapped(headers, config.PriceColumn);
        var rows = new List<ProductReferenceRow>(records.Count - 1);
        foreach (var record in records.Skip(1))
        {
            rows.Add(new ProductReferenceRow(
                Value(record, barcode),
                Value(record, name),
                Value(record, unit),
                Value(record, category),
                Value(record, manufacturer),
                DecimalValue(record, packQty),
                DecimalValue(record, price),
                config.SheetName));
        }
        return rows;
    }

    private static int Required(IReadOnlyDictionary<string, int> headers, string name)
    {
        if (headers.TryGetValue(name.Trim(), out var index)) return index;
        throw new BusinessRuleException($"Google Sheets varag'ida '{name}' ustuni topilmadi.", "product_reference_column_missing");
    }

    private static int? Mapped(IReadOnlyDictionary<string, int> headers, string name) =>
        string.IsNullOrWhiteSpace(name) ? null : Required(headers, name);

    private static string? Value(IReadOnlyList<string> row, int? index) =>
        index is { } value && value < row.Count ? row[value] : null;

    private static decimal? DecimalValue(IReadOnlyList<string> row, int? index)
    {
        var value = Value(row, index)?.Trim().Replace("\u00A0", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(value)) return null;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)) return result;
        if (!value.Contains('.', StringComparison.Ordinal)
            && decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out result)) return result;
        return null;
    }
}

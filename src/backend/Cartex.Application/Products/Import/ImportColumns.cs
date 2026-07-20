using System.Globalization;

namespace Cartex.Application.Products.Import;

public enum ImportField
{
    Name,
    Barcode,
    PackQty,
    Sku,
    Category,
    Unit,
    SellingPrice,
    PurchasePrice,
    Quantity,
    ExpiredAt,
    MinStock,
    Ikpu,
    Vat,
    ImageUrl,
    Currency
}

public static class ImportColumns
{
    private static readonly Dictionary<ImportField, string[]> Aliases = new()
    {
        [ImportField.Name] = ["nomi", "nom", "mahsulot", "mahsulotnomi", "tovar", "tovarnomi", "маҳсулот", "номи", "наименование", "название", "товар", "name", "product", "productname", "title"],
        [ImportField.Barcode] = ["barkod", "shtrixkod", "баркод", "штрихкод", "штрих", "штриховойкод", "barcode", "ean", "ean13", "upc"],
        [ImportField.PackQty] = ["pachka", "pachkasoni", "pachkadagisoni", "paket", "quti", "упаковка", "вупаковке", "коробка", "pack", "packqty", "packsize"],
        [ImportField.Sku] = ["artikul", "kod", "sku", "артикул", "код", "code"],
        [ImportField.Category] = ["kategoriya", "turkum", "guruh", "категория", "группа", "category", "group"],
        [ImportField.Unit] = ["birlik", "olchov", "olchovbirligi", "birligi", "бирлик", "единица", "единицаизмерения", "едизм", "unit", "uom", "measure"],
        [ImportField.SellingPrice] = ["sotishnarxi", "narx", "narxi", "sotuvnarxi", "chakananarx", "нарх", "цена", "ценапродажи", "розничнаяцена", "продажа", "price", "sellprice", "sellingprice", "retailprice"],
        [ImportField.PurchasePrice] = ["kirimnarxi", "tannarx", "tannarxi", "xaridnarxi", "kelishnarxi", "таннарх", "ценаприхода", "закупка", "закупочнаяцена", "приход", "себестоимость", "purchaseprice", "cost", "costprice", "buyprice"],
        [ImportField.Quantity] = ["soni", "miqdor", "miqdori", "qoldiq", "сони", "количество", "колво", "остаток", "quantity", "qty", "stock", "amount"],
        [ImportField.ExpiredAt] = ["yaroqlilikmuddati", "muddat", "muddati", "amalqilishmuddati", "муддат", "срокгодности", "срок", "годендо", "expiry", "expirydate", "expiredat", "bestbefore"],
        [ImportField.MinStock] = ["minqoldiq", "minimalqoldiq", "minsoni", "минимальныйостаток", "минзапас", "minstock", "minimum"],
        [ImportField.Ikpu] = ["ikpu", "mxik", "икпу", "мхик"],
        [ImportField.Vat] = ["qqs", "nds", "ккс", "ндс", "vat", "tax"],
        [ImportField.Currency] = ["valyuta", "valuta", "валюта", "currency"],
        [ImportField.ImageUrl] = ["suraturl", "surat", "rasm", "rasmurl", "сурат", "расм", "фото", "изображение", "картинка", "imageurl", "image", "photo", "picture", "imagelink"]
    };

    public static Dictionary<int, ImportField> Detect(IReadOnlyList<string> header)
    {
        var mapping = new Dictionary<int, ImportField>();
        var taken = new HashSet<ImportField>();

        for (var i = 0; i < header.Count; i++)
        {
            var key = Normalize(header[i]);
            if (key.Length == 0)
                continue;

            foreach (var (field, aliases) in Aliases)
            {
                if (taken.Contains(field) || !aliases.Contains(key))
                    continue;
                mapping[i] = field;
                taken.Add(field);
                break;
            }
        }

        return mapping;
    }

    public static Dictionary<int, ImportField> Parse(string mapping)
    {
        var result = new Dictionary<int, ImportField>();
        foreach (var part in mapping.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split(':', 2);
            if (pair.Length == 2 && int.TryParse(pair[0], out var index) && index >= 0 && Enum.TryParse<ImportField>(pair[1], true, out var field))
                result[index] = field;
        }
        return result;
    }

    public static string Normalize(string? header) =>
        header is null ? string.Empty : new string([.. header.Where(char.IsLetterOrDigit)]).ToLowerInvariant();

    public static decimal? Number(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = new string([.. raw.Where(c => !char.IsWhiteSpace(c) && c != '\'')]);
        var comma = text.LastIndexOf(',');
        var dot = text.LastIndexOf('.');

        if (comma >= 0 && dot >= 0)
            text = comma > dot
                ? text.Replace(".", string.Empty).Replace(',', '.')
                : text.Replace(",", string.Empty);
        else if (comma >= 0)
            text = text.Replace(',', '.');

        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd", "dd-MM-yyyy"];

    public static ImportRowDto ReadRow(int number, IReadOnlyList<string> cells, Dictionary<int, ImportField> mapping)
    {
        var values = new Dictionary<ImportField, string>();
        foreach (var (index, field) in mapping)
            if (index >= 0 && index < cells.Count && !string.IsNullOrWhiteSpace(cells[index]))
                values[field] = cells[index].Trim();

        var warnings = new List<string>();

        string? Text(ImportField field) => values.GetValueOrDefault(field);

        decimal? Num(ImportField field)
        {
            if (Text(field) is not { } raw)
                return null;
            var value = Number(raw);
            if (value is null)
                warnings.Add($"Son o'qilmadi: {raw}");
            return value;
        }

        DateOnly? Day(ImportField field)
        {
            if (Text(field) is not { } raw)
                return null;
            var value = Date(raw);
            if (value is null)
                warnings.Add($"Sana o'qilmadi: {raw}");
            return value;
        }

        return new ImportRowDto(
            number,
            Text(ImportField.Name),
            Text(ImportField.Barcode),
            Num(ImportField.PackQty),
            Text(ImportField.Sku),
            Text(ImportField.Category),
            Text(ImportField.Unit),
            Num(ImportField.SellingPrice),
            Num(ImportField.PurchasePrice),
            Num(ImportField.Quantity),
            Day(ImportField.ExpiredAt),
            Num(ImportField.MinStock),
            Text(ImportField.Ikpu),
            Num(ImportField.Vat),
            Text(ImportField.ImageUrl),
            Text(ImportField.Currency),
            null,
            ImportRowAction.Create,
            [],
            warnings);
    }

    public static DateOnly? Date(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        return DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date : null;
    }
}

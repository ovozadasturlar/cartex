using System.Globalization;
using System.Text.RegularExpressions;

namespace Cartex.Application.Common.Search;

public enum CatalogSearchField
{
    Any,
    Name,
    Barcode,
    Code,
    Price
}

public sealed record CatalogSearchTerm(CatalogSearchField Field, string Value)
{
    public decimal? Price => Field == CatalogSearchField.Price && decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
        ? value
        : null;
}

public sealed record CatalogSearch(IReadOnlyList<CatalogSearchTerm> Terms)
{
    private static readonly Regex FieldPattern = new("(?<field>[^\\s:]+):(?<value>\"[^\"]+\"|[^\\s]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CatalogSearch Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new CatalogSearch([]);

        var terms = new List<CatalogSearchTerm>();
        var consumed = new List<(int Index, int Length)>();

        foreach (Match match in FieldPattern.Matches(text))
        {
            if (!TryField(match.Groups["field"].Value, out var field))
                continue;

            var value = Unquote(match.Groups["value"].Value);
            if (value.Length == 0)
                continue;

            terms.Add(new CatalogSearchTerm(field, value));
            consumed.Add((match.Index, match.Length));
        }

        var plain = text.ToCharArray();
        foreach (var (index, length) in consumed)
            Array.Fill(plain, ' ', index, length);

        foreach (var token in new string(plain).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            terms.Add(new CatalogSearchTerm(CatalogSearchField.Any, Unquote(token)));

        return new CatalogSearch(terms);
    }

    private static bool TryField(string alias, out CatalogSearchField field)
    {
        field = alias.ToLowerInvariant() switch
        {
            "name" or "nomi" or "nom" => CatalogSearchField.Name,
            "barcode" or "barkod" or "shtrix" or "shtrixkod" => CatalogSearchField.Barcode,
            "code" or "sku" or "kod" or "ikpu" => CatalogSearchField.Code,
            "price" or "narx" => CatalogSearchField.Price,
            _ => CatalogSearchField.Any
        };

        return field != CatalogSearchField.Any;
    }

    private static string Unquote(string value) => value.Length > 1 && value[0] == '"' && value[^1] == '"'
        ? value[1..^1]
        : value;
}

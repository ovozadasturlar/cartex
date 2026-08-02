namespace Cartex.Shared.Models.Common;

public static class CurrencyCatalog
{
    public static readonly string[] All = ["UZS", "USD", "EUR", "RUB", "KZT", "TRY", "CNY"];

    public static CurrencyMetadata Resolve(string? code) => (code ?? "UZS").ToUpperInvariant() switch
    {
        "UZS" => new("UZS", "so'm", "Suffix", 0),
        "USD" => new("USD", "$", "Prefix", 2),
        "EUR" => new("EUR", "€", "Prefix", 2),
        "RUB" => new("RUB", "₽", "Suffix", 2),
        "KZT" => new("KZT", "₸", "Suffix", 2),
        "TRY" => new("TRY", "₺", "Prefix", 2),
        "CNY" => new("CNY", "¥", "Prefix", 2),
        var value => new(value, value, "Suffix", 2)
    };

    public static string Format(decimal amount, string? code, string? symbol = null, string? position = null, int? decimalDigits = null)
    {
        var metadata = Resolve(code);
        var resolvedSymbol = string.IsNullOrWhiteSpace(symbol) ? metadata.Symbol : symbol.Trim();
        var resolvedPosition = position is "Prefix" or "Suffix" ? position : metadata.SymbolPosition;
        var digits = Math.Clamp(decimalDigits ?? metadata.DecimalDigits, 0, 4);
        var number = amount.ToString($"N{digits}");
        return resolvedPosition == "Prefix" ? $"{resolvedSymbol}{number}" : $"{number} {resolvedSymbol}";
    }
}

public sealed record CurrencyMetadata(string Code, string Symbol, string SymbolPosition, int DecimalDigits);

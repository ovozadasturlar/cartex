using Cartex.Shared.Models.Common;

namespace Cartex.UI.Services;

public static class BarcodeLabelFormatting
{
    public static string FormatPrice(
        decimal? amount,
        string? currencyCode,
        string? currencySymbol,
        string? symbolPosition,
        int? decimalDigits,
        PrinterSettings settings)
    {
        if (amount is null) return string.Empty;

        var metadata = CurrencyCatalog.Resolve(currencyCode);
        var useCode = string.Equals(settings.LabelCurrencyDisplay, "code", StringComparison.OrdinalIgnoreCase);
        var token = useCode
            ? metadata.Code
            : string.IsNullOrWhiteSpace(currencySymbol) ? metadata.Symbol : currencySymbol.Trim();
        token = settings.LabelCurrencyCase?.ToLowerInvariant() switch
        {
            "upper" => token.ToUpperInvariant(),
            "lower" => token.ToLowerInvariant(),
            _ => token
        };
        var digits = Math.Clamp(decimalDigits ?? metadata.DecimalDigits, 0, 4);
        var number = amount.Value.ToString($"N{digits}");
        var position = useCode ? "Suffix" : symbolPosition is "Prefix" or "Suffix" ? symbolPosition : metadata.SymbolPosition;
        return position == "Prefix" ? $"{token}{number}" : $"{number} {token}";
    }
}

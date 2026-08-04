using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Rates;

namespace Cartex.UI.Services;

public static class BarcodeLabelFormatting
{
    public static string FormatProductPrice(
        decimal? amount,
        string? currencyCode,
        string? currencySymbol,
        string? symbolPosition,
        int? decimalDigits,
        IReadOnlyList<CurrencyDto> currencies,
        PrinterSettings settings)
    {
        if (amount is null) return string.Empty;
        if (!string.Equals(settings.LabelPriceCurrencyMode, "default", StringComparison.OrdinalIgnoreCase))
            return FormatPrice(amount, currencyCode, currencySymbol, symbolPosition, decimalDigits, settings);

        var baseCurrency = currencies.FirstOrDefault(x => x.IsBase);
        if (baseCurrency is null) return string.Empty;
        var source = currencies.FirstOrDefault(x => string.Equals(x.Code, currencyCode, StringComparison.OrdinalIgnoreCase));
        var rate = string.Equals(currencyCode, baseCurrency.Code, StringComparison.OrdinalIgnoreCase)
            ? 1m
            : source?.Rate;
        if (rate is null or <= 0) return string.Empty;
        return FormatPrice(
            Math.Round(amount.Value * rate.Value, Math.Clamp(baseCurrency.DecimalDigits, 0, 4)),
            baseCurrency.Code,
            baseCurrency.Symbol,
            baseCurrency.SymbolPosition,
            baseCurrency.DecimalDigits,
            settings);
    }

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

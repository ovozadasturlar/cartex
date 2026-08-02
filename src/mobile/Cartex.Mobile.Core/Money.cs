using System.Globalization;
using Cartex.Shared.Models.Common;

namespace Cartex.Mobile.Core;

public static class Money
{
    private static readonly NumberFormatInfo Format = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = ",", NumberDecimalDigits = 0 };

    public static string Text(decimal amount) => amount.ToString("N0", Format);

    public static string Text(decimal amount, string? currency) =>
        string.IsNullOrEmpty(currency) ? Text(amount) : $"{Text(amount)} {currency}";

    public static string Currency(decimal amount, string? currency, string? symbol = null, string? position = null, int? decimalDigits = null) =>
        CurrencyCatalog.Format(amount, currency, symbol, position, decimalDigits);

    public static string Quantity(decimal value) =>
        value == Math.Truncate(value) ? value.ToString("N0", Format) : value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string QuantityWithUnit(decimal value, string? unit) =>
        string.IsNullOrEmpty(unit) ? Quantity(value) : $"{Quantity(value)} {unit}";

    public static decimal Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var clean = new string([.. text.Where(c => char.IsDigit(c) || c is '.' or ',')]).Replace(',', '.');
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}

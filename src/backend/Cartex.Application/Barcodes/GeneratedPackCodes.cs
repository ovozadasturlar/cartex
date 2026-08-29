using System.Globalization;
using System.Text.RegularExpressions;

namespace Cartex.Application.Barcodes;

public static partial class GeneratedPackCodes
{
    // A scanned barcode is untrusted input: a runaway match must give up, not hang the till.
    [GeneratedRegex(@"-P(\d+(?:\.\d+)?)-", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex Marker();

    public static decimal? EmbeddedQty(string code)
    {
        var match = Marker().Match(code);
        return match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)
            ? qty
            : null;
    }

    public static void EnsureConsistent(string code, decimal packQty)
    {
        if (EmbeddedQty(code) is { } embedded && embedded != packQty)
            throw new BusinessRuleException($"{code} kodida qadoq soni yozilgan (P{embedded:0.###}) — boshqa son bilan saqlab bo'lmaydi.");
    }
}

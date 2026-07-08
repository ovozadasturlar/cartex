using System.Globalization;
using System.Text.RegularExpressions;
using Cartex.Shared.Models.Products;

namespace Cartex.UI.Services;

public static class BarcodeSyntax
{
    private static readonly Regex PackMarker = new(@"-P(\d+(?:\.\d+)?)-", RegexOptions.Compiled);

    public static List<BarcodeInput> Parse(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token =>
            {
                var idx = token.LastIndexOf('*');
                if (idx > 0 && decimal.TryParse(token[(idx + 1)..], out var qty) && qty > 0)
                    return new BarcodeInput(token[..idx].Trim(), qty);
                return new BarcodeInput(token, EmbeddedQty(token) ?? 1);
            })
            .Where(b => b.Code.Length > 0)
            .ToList();

    public static string Format(IEnumerable<VariantBarcodeDto> barcodes) =>
        string.Join(", ", barcodes.Select(b =>
            b.PackQty > 1 && EmbeddedQty(b.Code) != b.PackQty ? $"{b.Code}*{b.PackQty:0.###}" : b.Code));

    private static decimal? EmbeddedQty(string code)
    {
        var match = PackMarker.Match(code);
        return match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)
            ? qty
            : null;
    }
}

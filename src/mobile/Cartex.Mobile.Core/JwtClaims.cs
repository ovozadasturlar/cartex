using System.Text.Json;

namespace Cartex.Mobile.Core;

// Tokenni faqat o'qiydi — imzo serverda tekshiriladi, klient da'volarni UI uchun ishlatadi.
// `System.IdentityModel.Tokens.Jwt` o'rniga shu ishlatiladi: u ishga tushishda ~150 ms yeydi.
public sealed class JwtClaims
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Dictionary<string, List<string>> _claims;

    private JwtClaims(Dictionary<string, List<string>> claims)
    {
        _claims = claims;
        ValidTo = long.TryParse(First("exp"), out var exp) ? Epoch.AddSeconds(exp) : DateTime.MinValue;
    }

    public DateTime ValidTo { get; }

    public string? First(string type) => _claims.TryGetValue(type, out var list) ? list[0] : null;

    public long? Number(string type) => long.TryParse(First(type), out var value) ? value : null;

    public IEnumerable<string> All(string type) =>
        _claims.TryGetValue(type, out var list) ? list : [];

    public static JwtClaims? Parse(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            using var document = JsonDocument.Parse(Decode(parts[1]));
            var claims = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var values = claims.TryGetValue(property.Name, out var existing) ? existing : claims[property.Name] = [];
                if (property.Value.ValueKind == JsonValueKind.Array)
                    values.AddRange(property.Value.EnumerateArray().Select(Text));
                else
                    values.Add(Text(property.Value));
            }
            return new JwtClaims(claims);
        }
        catch
        {
            return null;
        }
    }

    private static string Text(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : element.ToString();

    private static byte[] Decode(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}

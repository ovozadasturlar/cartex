namespace Cartex.Application.Common;

public static class Phones
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        if (digits.Length == 9) return "+998" + digits;
        return "+" + digits;
    }

    public static bool IsValid(string? normalized) =>
        normalized is { Length: >= 11 and <= 16 } && normalized[0] == '+' && normalized.Skip(1).All(char.IsDigit);
}

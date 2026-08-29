namespace Cartex.Catalog.Tool.Normalization;

public sealed record BarcodeResult(string? Value, string? Issue, string? Detail);

public static class BarcodeNormalizer
{
    public const string MissingCode = "GKAT-10";
    public const string LengthCode = "GKAT-11";
    public const string CheckDigitCode = "GKAT-12";
    public const string InternalCode = "GKAT-13";
    public const string TwelveDigitCode = "GKAT-15";
    public const string ContainerCode = "GKAT-16";
    public const string GeneratedCode = "GKAT-81";

    public static BarcodeResult Classify(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Any(char.IsLetter))
            return new BarcodeResult(null, GeneratedCode, $"'{text}' is a generated code and is not printed on the product");

        var digits = Digits(text);
        return digits.Length switch
        {
            0 => new BarcodeResult(null, MissingCode, "barcode is empty"),
            13 => Ean13(digits),
            14 => Itf14(digits),
            12 => new BarcodeResult(null, TwelveDigitCode,
                $"{digits} has 12 digits; padding it with a zero would forge a plausible but wrong key"),
            7 or 8 => new BarcodeResult(null, InternalCode,
                $"{digits} is a manufacturer's internal code and is not printed on the product"),
            _ => new BarcodeResult(null, LengthCode, $"{digits.Length} digits is neither an ean13 nor an itf14")
        };
    }

    public static string Digits(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.EndsWith(".0", StringComparison.Ordinal))
            text = text[..^2];

        return string.Concat(text.Where(char.IsAsciiDigit));
    }

    public static char CheckDigit(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
            sum += (body[^(i + 1)] - '0') * (i % 2 == 0 ? 3 : 1);

        return (char)('0' + (10 - sum % 10) % 10);
    }

    private static BarcodeResult Ean13(string digits) =>
        CheckDigit(digits[..12]) == digits[12]
            ? new BarcodeResult(digits, null, null)
            : new BarcodeResult(null, CheckDigitCode, $"check digit of {digits} is wrong");

    private static BarcodeResult Itf14(string digits)
    {
        if (CheckDigit(digits[..13]) != digits[13])
            return new BarcodeResult(null, ContainerCode, $"check digit of {digits} is wrong for a 14-digit code");

        var body = digits[1..13];
        return new BarcodeResult(body + CheckDigit(body), null, null);
    }
}

namespace Cartex.Application.Common.Extensions;

using System.Globalization;
using System.Text.Json;

public static class ConversionHelper
{
    private static readonly string[] DateFormats = [
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "dd.MM.yyyy HH:mm:ss",
        "dd.MM.yyyy",
        "d.M.yyyy",
    ];

    public static bool TryParseFlexibleDateTimeOffset(string input, out DateTimeOffset result)
    {
        foreach (var format in DateFormats)
            if (DateTimeOffset.TryParseExact(input, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;

        return DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    public static DateTimeOffset ParseFlexibleDateTimeOffset(string input) =>
        TryParseFlexibleDateTimeOffset(input, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Cannot parse date: '{input}'");

    public static object? TryConvert(object value, Type targetType)
    {
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is JsonElement json)
            value = json.ValueKind switch
            {
                JsonValueKind.String => json.GetString()!,
                JsonValueKind.Number => json.TryGetInt64(out var l) ? l : json.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => json.ToString()
            };

        var str = value?.ToString();
        if (string.IsNullOrWhiteSpace(str)) return null;

        if (targetType == typeof(Guid)) return Guid.TryParse(str, out var guid) ? guid : null;
        if (targetType == typeof(DateTime)) return TryParseFlexibleDateTimeOffset(str, out var dt) ? dt.DateTime : null;
        if (targetType == typeof(DateTimeOffset)) return TryParseFlexibleDateTimeOffset(str, out var dto) ? dto : null;
        if (targetType.IsEnum) return Enum.TryParse(targetType, str, ignoreCase: true, out var en) ? en : null;

        try
        {
            return value is IConvertible ? Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture) : null;
        }
        catch
        {
            return null;
        }
    }
}

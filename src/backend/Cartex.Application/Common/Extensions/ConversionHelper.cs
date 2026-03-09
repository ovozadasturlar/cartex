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

    public static DateTimeOffset ParseFlexibleDateTimeOffset(string input)
    {
        foreach (var format in DateFormats)
            if (DateTimeOffset.TryParseExact(input, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;

        if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
            return fallback;

        throw new InvalidOperationException($"Cannot parse date: '{input}'");
    }

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

        if (targetType == typeof(Guid)) return Guid.Parse(str);
        if (targetType == typeof(DateTime)) return ParseFlexibleDateTimeOffset(str).DateTime;
        if (targetType == typeof(DateTimeOffset)) return ParseFlexibleDateTimeOffset(str);
        if (targetType.IsEnum) return Enum.Parse(targetType, str, ignoreCase: true);
        if (value is IConvertible) return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);

        throw new InvalidOperationException($"Cannot convert '{value}' to {targetType.Name}");
    }
}

namespace Cartex.Application.Common.Extensions;

public static class DateTimeExtensions
{
    /// Npgsql rejects a DateTime whose Kind is not Utc for `timestamp with time zone`,
    /// and model binding produces Unspecified for every date query parameter.
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime? AsUtc(this DateTime? value) => value?.AsUtc();
}

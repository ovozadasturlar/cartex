namespace Cartex.ApiClient;

public static class ServerClock
{
    private static long _offsetTicks;

    public static TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));
    public static DateTime UtcNow => DateTime.UtcNow + Offset;

    public static void Update(DateTimeOffset serverTime) =>
        Interlocked.Exchange(ref _offsetTicks, (serverTime.UtcDateTime - DateTime.UtcNow).Ticks);

    public static bool IsExpiringSoon(DateTime validTo) => validTo <= UtcNow.AddSeconds(60);
}

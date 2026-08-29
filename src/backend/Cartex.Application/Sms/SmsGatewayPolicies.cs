using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Entities;
using Cartex.Persistence;

namespace Cartex.Application.Sms;

public static class SmsGatewayConsentPolicy
{
    public static bool Expands(SmsGatewayDevice device, int? quota, int minIntervalSeconds, int maxPerHour) =>
        device.MonthlyQuota is not null && (quota is null || quota > device.MonthlyQuota)
        || minIntervalSeconds < device.MinIntervalSeconds
        || maxPerHour > device.MaxPerHour;
}

public static class SmsTestModePolicy
{
    public static bool ShouldSimulate(SmsSettings settings, string phone)
    {
        if (!settings.TestMode)
            return false;
        var normalized = NormalizePhone(phone);
        return !(settings.TestAllowedNumbers ?? []).Any(x => NormalizePhone(x) == normalized);
    }

    public static string NormalizePhone(string phone) => Cartex.Shared.Models.SmsGateway.SmsPhoneNumber.Normalize(phone);
}

public static class SmsGatewayState
{
    public static string BlockReason(SmsGatewayDevice device, int sentLastHour, DateTime now)
    {
        SmsGatewayRoutingService.ResetQuota(device, now);
        if (!device.IsTrusted)
            return "trust_required";
        if (!device.IsConsented)
            return "consent_required";
        if (!device.IsEnabled)
            return "disabled";
        if (device.PausedAt is not null)
            return "paused";
        if (device.LastSeenAt is not DateTime lastSeen || lastSeen < now.AddSeconds(-90))
            return "offline";
        if (device.MonthlyQuota is int quota && device.SentThisPeriod >= quota)
            return "quota_exhausted";
        if (device.LastSentAt is DateTime lastSent && lastSent.AddSeconds(device.MinIntervalSeconds) > now)
            return "interval_limited";
        if (sentLastHour >= device.MaxPerHour)
            return "hour_limited";
        return "active";
    }

    public static DateTime QuotaResetsAt(SmsGatewayDevice device, DateTime now)
    {
        var day = Math.Min(device.QuotaResetDay, DateTime.DaysInMonth(now.Year, now.Month));
        var reset = new DateTime(now.Year, now.Month, day, 0, 0, 0, DateTimeKind.Utc);
        if (reset > now)
            return reset;
        var next = now.AddMonths(1);
        return new DateTime(next.Year, next.Month,
            Math.Min(device.QuotaResetDay, DateTime.DaysInMonth(next.Year, next.Month)), 0, 0, 0, DateTimeKind.Utc);
    }
}

public sealed class SmsQuotaWarningService(
    IApplicationDbContext db,
    ISettingsService settings,
    INotificationService notifications)
{
    public async Task NotifyIfNeededAsync(SmsGatewayDevice device, CancellationToken cancellationToken)
    {
        if (device.MonthlyQuota is not int quota || quota <= 0
            || device.LowQuotaWarnedPeriodStartedAt == device.PeriodStartedAt)
            return;
        var remainingPercent = Math.Max(0L, (long)quota - device.SentThisPeriod) * 100 / quota;
        if (remainingPercent >= device.LowQuotaWarnPercent)
            return;
        var telegram = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
        if (telegram is not { Enabled: true } || string.IsNullOrWhiteSpace(telegram.ChatId))
            return;
        var message = new NotificationMessage(
            Cartex.Domain.Enums.NotificationChannel.Telegram,
            telegram.ChatId,
            "sms_quota_low",
            new Dictionary<string, string>
            {
                ["device"] = device.DeviceName,
                ["remaining"] = Math.Max(0, quota - device.SentThisPeriod).ToString(),
                ["limit"] = quota.ToString()
            });
        device.LowQuotaWarnedPeriodStartedAt = device.PeriodStartedAt;
        await db.SaveChangesAsync(cancellationToken);
        await db.RunAfterCommitAsync(() => notifications.SendAsync(message, cancellationToken));
    }
}

using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sms;

public sealed class SmsGatewayRoutingService(
    IApplicationDbContext db,
    ISettingsService settingsService,
    ISmsGatewayNotifier notifier)
{
    public async Task<bool> AssignAsync(SmsGatewayJob job, CancellationToken cancellationToken)
    {
        if (job.Status != SmsGatewayJobStatus.Pending)
            return false;
        var now = DateTime.UtcNow;
        if (job.AvailableAt > now)
            return false;
        var settings = await settingsService.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new();
        var quietUntil = SmsQuietHoursPolicy.NextAllowedUtc(settings, job.Kind, DateTime.Now);
        if (quietUntil is not null)
        {
            Wait(job, "quiet_hours", quietUntil);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        var devices = await db.SmsGatewayDevices
            .Where(x => x.BranchId == job.BranchId)
            .ToListAsync(cancellationToken);
        foreach (var device in devices)
            ResetQuota(device, now);
        var since = now.AddHours(-1);
        var sentCounts = await db.SmsGatewayJobs.AsNoTracking()
            .Where(x => x.BranchId == job.BranchId && x.AssignedDeviceId != null && x.SentAt >= since)
            .GroupBy(x => x.AssignedDeviceId!.Value)
            .Select(x => new { DeviceId = x.Key, Count = x.Sum(j => j.SegmentCount) })
            .ToDictionaryAsync(x => x.DeviceId, x => x.Count, cancellationToken);

        var route = job.CustomerId is long customerId
            ? await db.CustomerSmsRoutes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.BranchId == job.BranchId && x.CustomerId == customerId, cancellationToken)
            : null;
        var stickyId = job.StickyDeviceId ?? route?.LastDeviceId;
        var sticky = stickyId is long id ? devices.FirstOrDefault(x => x.Id == id) : null;
        if (sticky is not null && CanSend(sticky, job, sentCounts.GetValueOrDefault(sticky.Id), now))
            return await AssignAsync(job, sticky, now, cancellationToken);

        var stickyWait = StickyWaitMinutes(settings, job.Kind);
        if (sticky is not null && stickyWait > 0)
        {
            var until = job.WaitingReason == "sticky_device" && job.AvailableAt is DateTime availableAt
                ? availableAt
                : job.CreatedAt.AddMinutes(stickyWait);
            if (until > now)
            {
                job.StickyDeviceId = sticky.Id;
                Wait(job, "sticky_device", until);
                await db.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        var candidates = devices
            .Where(x => CanSend(x, job, sentCounts.GetValueOrDefault(x.Id), now))
            .OrderBy(x => x.MonthlyQuota is null)
            .ThenByDescending(RemainingQuotaRatio)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToList();
        if (candidates.Count > 0)
            return await AssignAsync(job, candidates[0], now, cancellationToken);

        var active = devices.Where(x => IsActive(x, now)).ToList();
        job.WaitingReason = active.Count == 0
            ? "no_device"
            : active.All(x => !HasQuota(x, job))
                ? "quota_exhausted"
                : "rate_limited";
        job.AvailableAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return false;
    }

    public async Task<bool> AssignToAsync(SmsGatewayJob job, long deviceId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var device = await db.SmsGatewayDevices.FirstOrDefaultAsync(
            x => x.Id == deviceId && x.BranchId == job.BranchId, cancellationToken);
        if (device is null)
            return false;
        ResetQuota(device, now);
        var sentLastHour = await db.SmsGatewayJobs.AsNoTracking()
            .Where(x => x.AssignedDeviceId == device.Id && x.SentAt >= now.AddHours(-1))
            .SumAsync(x => (int?)x.SegmentCount, cancellationToken) ?? 0;
        return CanSend(device, job, sentLastHour, now)
            && await AssignAsync(job, device, now, cancellationToken);
    }

    private async Task<bool> AssignAsync(
        SmsGatewayJob job,
        SmsGatewayDevice device,
        DateTime now,
        CancellationToken cancellationToken)
    {
        job.Status = SmsGatewayJobStatus.Assigned;
        job.AssignedDeviceId = device.Id;
        job.LeaseToken = Guid.NewGuid().ToString("N");
        job.LeaseExpiresAt = now.AddMinutes(2);
        job.AssignedAt = now;
        job.AttemptCount++;
        job.ErrorCode = null;
        job.ErrorMessage = null;
        job.WaitingReason = null;
        job.AvailableAt = null;
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyJobAvailableAsync(device.DeviceId, device.SimSlot, job.Id, cancellationToken);
        return true;
    }

    private static bool CanSend(SmsGatewayDevice device, SmsGatewayJob job, int sentLastHour, DateTime now) =>
        IsActive(device, now)
        && HasQuota(device, job)
        && (device.LastSentAt is not DateTime lastSent || lastSent.AddSeconds(device.MinIntervalSeconds) <= now)
        && sentLastHour + job.SegmentCount <= device.MaxPerHour;

    private static bool IsActive(SmsGatewayDevice device, DateTime now) =>
        device.IsTrusted && device.IsConsented && device.IsEnabled && device.PausedAt is null
        && device.LastSeenAt >= now.AddSeconds(-90);

    private static bool HasQuota(SmsGatewayDevice device, SmsGatewayJob job) =>
        device.MonthlyQuota is not int quota || device.SentThisPeriod + job.SegmentCount <= quota;

    private static decimal RemainingQuotaRatio(SmsGatewayDevice device) => device.MonthlyQuota is > 0
        ? (decimal)(device.MonthlyQuota.Value - device.SentThisPeriod) / device.MonthlyQuota.Value
        : decimal.MinValue;

    private static int StickyWaitMinutes(SmsSettings settings, SmsGatewayJobKind kind) => kind switch
    {
        SmsGatewayJobKind.DebtReminder => settings.DebtReminderStickyWaitMinutes,
        SmsGatewayJobKind.ReceiptLink => settings.ReceiptLinkStickyWaitMinutes,
        SmsGatewayJobKind.Promotion => settings.PromotionStickyWaitMinutes,
        SmsGatewayJobKind.Manual => settings.ManualStickyWaitMinutes,
        _ => throw new InvalidOperationException($"Noma'lum SMS turi: {kind}")
    };

    private static void Wait(SmsGatewayJob job, string reason, DateTime? until)
    {
        job.Status = SmsGatewayJobStatus.Pending;
        job.WaitingReason = reason;
        job.AvailableAt = until;
    }

    public static void ResetQuota(SmsGatewayDevice device, DateTime now)
    {
        var day = Math.Min(device.QuotaResetDay, DateTime.DaysInMonth(now.Year, now.Month));
        var start = new DateTime(now.Year, now.Month, day, 0, 0, 0, DateTimeKind.Utc);
        if (start > now)
        {
            var previous = now.AddMonths(-1);
            start = new DateTime(previous.Year, previous.Month,
                Math.Min(device.QuotaResetDay, DateTime.DaysInMonth(previous.Year, previous.Month)), 0, 0, 0, DateTimeKind.Utc);
        }
        if (device.PeriodStartedAt >= start)
            return;
        device.PeriodStartedAt = start;
        device.SentThisPeriod = 0;
        device.LowQuotaWarnedPeriodStartedAt = null;
    }
}

public static class SmsQuietHoursPolicy
{
    public static DateTime? NextAllowedUtc(SmsSettings settings, SmsGatewayJobKind kind, DateTime localNow)
    {
        if (!settings.QuietHoursEnabled || kind is SmsGatewayJobKind.ReceiptLink or SmsGatewayJobKind.Manual)
            return null;
        if (!TimeOnly.TryParseExact(settings.SendWindowStart, "HH:mm", out var start)
            || !TimeOnly.TryParseExact(settings.SendWindowEnd, "HH:mm", out var end))
            return null;
        var time = TimeOnly.FromDateTime(localNow);
        var inside = start <= end ? time >= start && time < end : time >= start || time < end;
        if (inside)
            return null;
        var date = time < start ? localNow.Date : localNow.Date.AddDays(1);
        return DateTime.SpecifyKind(date + start.ToTimeSpan(), DateTimeKind.Local).ToUniversalTime();
    }
}

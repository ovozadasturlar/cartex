using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.SmsGateway;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKind = Cartex.Shared.Models.SmsGateway.SmsGatewayJobKind;
using SharedStatus = Cartex.Shared.Models.SmsGateway.SmsGatewayJobStatus;
using Unit = Cartex.Application.Common.Messaging.Unit;
using SmsGatewayJobKind = Cartex.Domain.Enums.SmsGatewayJobKind;
using SmsGatewayJobStatus = Cartex.Domain.Enums.SmsGatewayJobStatus;

namespace Cartex.Application.Sms;

public record RegisterSmsGatewayCommand(RegisterSmsGatewayRequest Request) : ICommand<RegisterSmsGatewayResult>;
public record HeartbeatSmsGatewayCommand(SmsGatewayHeartbeatRequest Request) : ICommand<Unit>;
public record SetSmsGatewayTrustCommand(long Id, SetSmsGatewayTrustRequest Request) : ICommand<Unit>;
public record UpdateSmsGatewayDeviceCommand(long Id, UpdateSmsGatewayDeviceRequest Request) : ICommand<Unit>;
public record SetSmsGatewayConsentCommand(long Id, SetSmsGatewayConsentRequest Request) : ICommand<Unit>;
public record UpdateSmsGatewayConsentCommand(long Id, UpdateSmsGatewayConsentRequest Request) : ICommand<UpdateSmsGatewayConsentResult>;
public record SetSmsGatewayPauseCommand(long Id, SetSmsGatewayPauseRequest Request) : ICommand<Unit>;
public record GetSmsGatewayDevicesQuery(long BranchId) : IRequest<IReadOnlyList<SmsGatewayDeviceDto>>;
public record GetSmsGatewayHostStateQuery(SmsGatewayHostStateRequest Request) : IRequest<SmsGatewayHostStateDto>;
public record GetSmsGatewayJobsQuery(long BranchId, int Take = 100) : IRequest<IReadOnlyList<SmsGatewayJobDto>>;
public record GetAssignedSmsGatewayJobsQuery(string DeviceId, int SimSlot, string HostToken) : IRequest<IReadOnlyList<AssignedSmsGatewayJobDto>>;
public record MarkSmsGatewayJobSentCommand(long Id, SmsGatewayLeaseRequest Request) : ICommand<Unit>;
public record MarkSmsGatewayJobDeliveredCommand(long Id, SmsGatewayLeaseRequest Request) : ICommand<Unit>;
public record MarkSmsGatewayJobSimulatedCommand(long Id, SmsGatewayLeaseRequest Request) : ICommand<Unit>;
public record FailSmsGatewayJobCommand(long Id, SmsGatewayFailureRequest Request) : ICommand<Unit>;
public record CreateSmsGatewayJobCommand(CreateSmsGatewayJobRequest Request) : ICommand<SmsGatewayJobDto?>;
public record SendSmsGatewayTestCommand(SendSmsGatewayTestRequest Request) : ICommand<SmsGatewayJobDto?>;

public sealed class RegisterSmsGatewayCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditService audit)
    : IRequestHandler<RegisterSmsGatewayCommand, RegisterSmsGatewayResult>
{
    public async Task<RegisterSmsGatewayResult> Handle(RegisterSmsGatewayCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureHost(currentUser);
        SmsGatewayAccess.EnsureBranch(currentUser, command.Request.BranchId);
        var request = command.Request;
        var device = await db.SmsGatewayDevices.FirstOrDefaultAsync(x => x.BranchId == request.BranchId
            && x.DeviceId == request.DeviceId && x.SimSlot == request.SimSlot, cancellationToken);
        string? issuedToken = null;
        if (device is null)
        {
            var consent = request.Consent ?? throw new BusinessRuleException("SMS roziligi sozlamalari kerak.");
            issuedToken = SmsGatewayCredential.Issue();
            device = new SmsGatewayDevice
            {
                BranchId = request.BranchId,
                DeviceId = request.DeviceId,
                CredentialHash = SmsGatewayCredential.Hash(issuedToken),
                DeviceName = request.DeviceName,
                Client = request.Client,
                SimSlot = request.SimSlot,
                SimOperator = request.SimOperator,
                SimSubscriptionId = request.SimSubscriptionId,
                PhoneLabel = request.PhoneLabel,
                IsConsented = true,
                MonthlyQuota = consent.IsUnlimited ? null : consent.MonthlyQuota,
                ConsentedMonthlyQuota = consent.IsUnlimited ? null : consent.MonthlyQuota,
                QuotaResetDay = consent.QuotaResetDay,
                MaxPerHour = consent.MaxPerHour,
                ConsentedMaxPerHour = consent.MaxPerHour,
                MinIntervalSeconds = consent.MinIntervalSeconds,
                ConsentedMinIntervalSeconds = consent.MinIntervalSeconds,
                LowQuotaWarnPercent = consent.LowQuotaWarnPercent,
                ConsentedAt = DateTime.UtcNow,
                LastUserId = currentUser.UserId
            };
            db.SmsGatewayDevices.Add(device);
            audit.Add("sms.gateway_registered", "sms_gateway_devices", null, new
            {
                request.DeviceId,
                request.SimSubscriptionId,
                device.MonthlyQuota,
                device.QuotaResetDay,
                device.MaxPerHour,
                device.MinIntervalSeconds
            });
        }
        else if (string.IsNullOrWhiteSpace(request.HostToken))
        {
            // Telefon qayta o'rnatilganda lokal credential yo'qoladi. Yozuvni abadiy
            // qulflab qo'yish SIM shlyuzini tiklab bo'lmas holga keltiradi, shuning uchun
            // ruxsati bor foydalanuvchi qayta ro'yxatdan o'tkaza oladi: yangi credential
            // beriladi, SIM egasi rozilikni qaytadan beradi va do'kon egasi qaytadan
            // ishonch bildiradi.
            var consent = request.Consent ?? throw new BusinessRuleException("SMS roziligi sozlamalari kerak.");
            issuedToken = SmsGatewayCredential.Issue();
            device.CredentialHash = SmsGatewayCredential.Hash(issuedToken);
            device.CredentialIssuedAt = DateTime.UtcNow;
            device.DeviceName = request.DeviceName;
            device.Client = request.Client;
            device.SimOperator = request.SimOperator;
            device.SimSubscriptionId = request.SimSubscriptionId;
            device.PhoneLabel = request.PhoneLabel;
            device.IsTrusted = false;
            device.IsConsented = true;
            device.MonthlyQuota = consent.IsUnlimited ? null : consent.MonthlyQuota;
            device.ConsentedMonthlyQuota = consent.IsUnlimited ? null : consent.MonthlyQuota;
            device.QuotaResetDay = consent.QuotaResetDay;
            device.MaxPerHour = consent.MaxPerHour;
            device.ConsentedMaxPerHour = consent.MaxPerHour;
            device.MinIntervalSeconds = consent.MinIntervalSeconds;
            device.ConsentedMinIntervalSeconds = consent.MinIntervalSeconds;
            device.LowQuotaWarnPercent = consent.LowQuotaWarnPercent;
            device.ConsentedAt = DateTime.UtcNow;
            device.LastUserId = currentUser.UserId;
            audit.Add("sms.gateway_reregistered", "sms_gateway_devices", device.Id, new
            {
                request.DeviceId,
                request.SimSlot,
                device.MonthlyQuota,
                device.QuotaResetDay
            });
        }
        else
        {
            SmsGatewayCredential.Ensure(device, request.HostToken);
            device.DeviceName = request.DeviceName;
            device.Client = request.Client;
            device.SimSlot = request.SimSlot;
            device.SimOperator = request.SimOperator;
            device.SimSubscriptionId = request.SimSubscriptionId;
            device.PhoneLabel = request.PhoneLabel;
            device.LastUserId = currentUser.UserId;
        }
        device.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new RegisterSmsGatewayResult(SmsGatewayMapping.Device(device), issuedToken);
    }
}

public sealed class HeartbeatSmsGatewayCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<HeartbeatSmsGatewayCommand, Unit>
{
    public async Task<Unit> Handle(HeartbeatSmsGatewayCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureHost(currentUser);
        var device = await db.SmsGatewayDevices.FirstOrDefaultAsync(x => x.DeviceId == command.Request.DeviceId
            && x.SimSlot == command.Request.SimSlot, cancellationToken)
            ?? throw new NotFoundException("SMS shlyuzi topilmadi.");
        if (currentUser.DeviceId != command.Request.DeviceId)
            throw new ForbiddenException("Qurilma identifikatori mos emas.");
        SmsGatewayCredential.Ensure(device, command.Request.HostToken);
        device.LastSeenAt = DateTime.UtcNow;
        device.LastError = command.Request.LastError;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class SetSmsGatewayTrustCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, SmsGatewayRoutingService routing, IAuditService audit)
    : IRequestHandler<SetSmsGatewayTrustCommand, Unit>
{
    public async Task<Unit> Handle(SetSmsGatewayTrustCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        var device = await db.SmsGatewayDevices.FindAsync([command.Id], cancellationToken)
            ?? throw new NotFoundException("SMS shlyuzi topilmadi.");
        SmsGatewayAccess.EnsureBranch(currentUser, device.BranchId);
        device.IsTrusted = command.Request.IsTrusted;
        audit.Add(device.IsTrusted ? "sms.gateway_trusted" : "sms.gateway_untrusted", "sms_gateway_devices", device.Id,
            new { device.DeviceId, device.IsTrusted });
        var jobs = command.Request.IsTrusted ? [] : await SmsGatewayAccess.DetachJobsAsync(db, device.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var job in jobs)
            await routing.AssignAsync(job, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateSmsGatewayDeviceCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditService audit)
    : IRequestHandler<UpdateSmsGatewayDeviceCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSmsGatewayDeviceCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        var device = await db.SmsGatewayDevices.FindAsync([command.Id], cancellationToken)
            ?? throw new NotFoundException("SMS shlyuzi topilmadi.");
        SmsGatewayAccess.EnsureBranch(currentUser, device.BranchId);
        var old = new { device.IsEnabled, device.Priority };
        device.IsEnabled = command.Request.IsEnabled;
        device.Priority = command.Request.Priority;
        audit.Add("sms.gateway_owner_settings", "sms_gateway_devices", device.Id,
            new { device.IsEnabled, device.Priority }, old);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class SetSmsGatewayConsentCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, SmsGatewayRoutingService routing, IAuditService audit)
    : IRequestHandler<SetSmsGatewayConsentCommand, Unit>
{
    public async Task<Unit> Handle(SetSmsGatewayConsentCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureHost(currentUser);
        var device = await db.SmsGatewayDevices.FindAsync([command.Id], cancellationToken)
            ?? throw new NotFoundException("SMS shlyuzi topilmadi.");
        if (device.DeviceId != command.Request.DeviceId || device.SimSlot != command.Request.SimSlot
            || currentUser.DeviceId != command.Request.DeviceId)
            throw new ForbiddenException("Qurilma identifikatori mos emas.");
        SmsGatewayCredential.Ensure(device, command.Request.HostToken);
        device.IsConsented = command.Request.IsConsented;
        if (command.Request.IsConsented)
        {
            device.ConsentedAt = DateTime.UtcNow;
            audit.Add("sms.gateway_consent_granted", "sms_gateway_devices", device.Id,
                new { device.MonthlyQuota, device.QuotaResetDay, device.MaxPerHour, device.MinIntervalSeconds });
            await db.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
        audit.Add("sms.gateway_consent_revoked", "sms_gateway_devices", device.Id,
            oldData: new { device.MonthlyQuota, device.QuotaResetDay, device.MaxPerHour, device.MinIntervalSeconds });
        var jobs = await SmsGatewayAccess.DetachJobsAsync(db, device.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var job in jobs)
            await routing.AssignAsync(job, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateSmsGatewayConsentCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit)
    : IRequestHandler<UpdateSmsGatewayConsentCommand, UpdateSmsGatewayConsentResult>
{
    public async Task<UpdateSmsGatewayConsentResult> Handle(UpdateSmsGatewayConsentCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureHost(currentUser);
        var device = await SmsGatewayAccess.LoadOwnedDeviceAsync(db, currentUser, command.Id,
            command.Request.DeviceId, command.Request.SimSlot, command.Request.HostToken, cancellationToken);
        var scope = command.Request.Scope;
        var quota = scope.IsUnlimited ? null : scope.MonthlyQuota;
        var expands = SmsGatewayConsentPolicy.Expands(device, quota, scope.MinIntervalSeconds, scope.MaxPerHour);
        if (expands && !command.Request.ConsentGranted)
            return new UpdateSmsGatewayConsentResult(true, SmsGatewayMapping.Device(device));

        var old = new
        {
            device.MonthlyQuota,
            device.QuotaResetDay,
            device.MaxPerHour,
            device.MinIntervalSeconds,
            device.LowQuotaWarnPercent
        };
        device.MonthlyQuota = quota;
        device.ConsentedMonthlyQuota = quota;
        device.QuotaResetDay = scope.QuotaResetDay;
        device.MaxPerHour = scope.MaxPerHour;
        device.ConsentedMaxPerHour = scope.MaxPerHour;
        device.MinIntervalSeconds = scope.MinIntervalSeconds;
        device.ConsentedMinIntervalSeconds = scope.MinIntervalSeconds;
        device.LowQuotaWarnPercent = scope.LowQuotaWarnPercent;
        device.ConsentedAt = DateTime.UtcNow;
        audit.Add(expands ? "sms.gateway_consent_expanded" : "sms.gateway_consent_reduced",
            "sms_gateway_devices", device.Id, new
            {
                device.MonthlyQuota,
                device.QuotaResetDay,
                device.MaxPerHour,
                device.MinIntervalSeconds,
                device.LowQuotaWarnPercent
            }, old);
        await db.SaveChangesAsync(cancellationToken);
        return new UpdateSmsGatewayConsentResult(false, SmsGatewayMapping.Device(device));
    }
}

public sealed class SetSmsGatewayPauseCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditService audit)
    : IRequestHandler<SetSmsGatewayPauseCommand, Unit>
{
    public async Task<Unit> Handle(SetSmsGatewayPauseCommand command, CancellationToken cancellationToken)
    {
        var device = await SmsGatewayAccess.LoadOwnedDeviceAsync(db, currentUser, command.Id,
            command.Request.DeviceId, command.Request.SimSlot, command.Request.HostToken, cancellationToken);
        device.PausedAt = command.Request.IsPaused ? DateTime.UtcNow : null;
        audit.Add(command.Request.IsPaused ? "sms.gateway_paused" : "sms.gateway_resumed",
            "sms_gateway_devices", device.Id, new { command.Request.IsPaused });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class GetSmsGatewayDevicesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSmsGatewayDevicesQuery, IReadOnlyList<SmsGatewayDeviceDto>>
{
    public async Task<IReadOnlyList<SmsGatewayDeviceDto>> Handle(GetSmsGatewayDevicesQuery query, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        SmsGatewayAccess.EnsureBranch(currentUser, query.BranchId);
        var devices = await db.SmsGatewayDevices.Where(x => x.BranchId == query.BranchId)
            .OrderBy(x => x.Priority).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var device in devices)
            SmsGatewayRoutingService.ResetQuota(device, now);
        await db.SaveChangesAsync(cancellationToken);
        var since = DateTime.UtcNow.AddHours(-1);
        var counts = await db.SmsGatewayJobs.AsNoTracking()
            .Where(x => x.BranchId == query.BranchId && x.AssignedDeviceId != null && x.SentAt >= since)
            .GroupBy(x => x.AssignedDeviceId!.Value)
            .Select(x => new { DeviceId = x.Key, Count = x.Sum(j => j.SegmentCount) })
            .ToDictionaryAsync(x => x.DeviceId, x => x.Count, cancellationToken);
        var linked = await db.CustomerSmsRoutes.AsNoTracking().Where(x => x.BranchId == query.BranchId)
            .GroupBy(x => x.LastDeviceId).Select(x => new { DeviceId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.DeviceId, x => x.Count, cancellationToken);
        return devices.Select(x => SmsGatewayMapping.Device(x, counts.GetValueOrDefault(x.Id), linked.GetValueOrDefault(x.Id))).ToList();
    }
}

public sealed class GetSmsGatewayHostStateQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings)
    : IRequestHandler<GetSmsGatewayHostStateQuery, SmsGatewayHostStateDto>
{
    public async Task<SmsGatewayHostStateDto> Handle(GetSmsGatewayHostStateQuery query, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureHost(currentUser);
        var device = await db.SmsGatewayDevices
            .FirstOrDefaultAsync(x => x.DeviceId == query.Request.DeviceId && x.SimSlot == query.Request.SimSlot, cancellationToken)
            ?? throw new NotFoundException("SMS shlyuzi topilmadi.");
        if (currentUser.DeviceId != query.Request.DeviceId)
            throw new ForbiddenException("Qurilma identifikatori mos emas.");
        SmsGatewayCredential.Ensure(device, query.Request.HostToken);
        SmsGatewayRoutingService.ResetQuota(device, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        var sentLastHour = await db.SmsGatewayJobs.AsNoTracking()
            .Where(x => x.AssignedDeviceId == device.Id && x.SentAt >= DateTime.UtcNow.AddHours(-1))
            .SumAsync(x => (int?)x.SegmentCount, cancellationToken) ?? 0;
        var jobs = await db.SmsGatewayJobs.AsNoTracking()
            .Where(x => x.AssignedDeviceId == device.Id)
            .OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(cancellationToken);
        var sms = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new SmsSettings();
        var linkedCustomers = await db.CustomerSmsRoutes.CountAsync(x => x.LastDeviceId == device.Id, cancellationToken);
        return new SmsGatewayHostStateDto(SmsGatewayMapping.Device(device, sentLastHour, linkedCustomers),
            jobs.Select(x => SmsGatewayMapping.Job(x)).ToList(),
            new SmsGatewayMessageTypesDto(sms.DebtReminderEnabled, sms.ReceiptLinkEnabled,
                sms.ManualEnabled, sms.PromotionEnabled), sms.TestMode);
    }
}

public sealed class GetSmsGatewayJobsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSmsGatewayJobsQuery, IReadOnlyList<SmsGatewayJobDto>>
{
    public async Task<IReadOnlyList<SmsGatewayJobDto>> Handle(GetSmsGatewayJobsQuery query, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        SmsGatewayAccess.EnsureBranch(currentUser, query.BranchId);
        var jobs = await db.SmsGatewayJobs.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(x => x!.Party)
            .Include(x => x.AssignedDevice)
            .Where(x => x.BranchId == query.BranchId)
            .OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(query.Take, 1, 500)).ToListAsync(cancellationToken);
        return jobs.Select(x => SmsGatewayMapping.Job(x)).ToList();
    }
}

public sealed class GetAssignedSmsGatewayJobsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, ISettingsService settings)
    : IRequestHandler<GetAssignedSmsGatewayJobsQuery, IReadOnlyList<AssignedSmsGatewayJobDto>>
{
    public async Task<IReadOnlyList<AssignedSmsGatewayJobDto>> Handle(GetAssignedSmsGatewayJobsQuery query, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureHost(currentUser);
        var device = await db.SmsGatewayDevices.FirstOrDefaultAsync(x => x.DeviceId == query.DeviceId
            && x.SimSlot == query.SimSlot, cancellationToken)
            ?? throw new NotFoundException("SMS shlyuzi topilmadi.");
        SmsGatewayCredential.Ensure(device, query.HostToken);
        if (!device.IsTrusted || !device.IsConsented || !device.IsEnabled || device.PausedAt is not null)
            return [];
        var now = DateTime.UtcNow;
        var sms = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new SmsSettings();
        var jobs = await db.SmsGatewayJobs.AsNoTracking()
            .Where(x => x.AssignedDeviceId == device.Id && x.Status == SmsGatewayJobStatus.Assigned && x.LeaseExpiresAt > now)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return jobs.Select(x => new AssignedSmsGatewayJobDto(x.Id, (SharedKind)x.Kind, x.Phone, x.Text, x.SegmentCount,
            x.LeaseToken!, x.LeaseExpiresAt!.Value, device.MinIntervalSeconds, device.MaxPerHour,
            SmsTestModePolicy.ShouldSimulate(sms, x.Phone))).ToList();
    }
}

public sealed class MarkSmsGatewayJobSentCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    SmsQuotaWarningService quotaWarning,
    ISettingsService settings)
    : IRequestHandler<MarkSmsGatewayJobSentCommand, Unit>
{
    public async Task<Unit> Handle(MarkSmsGatewayJobSentCommand command, CancellationToken cancellationToken)
    {
        var (job, device) = await SmsGatewayAccess.LoadLeaseAsync(db, currentUser, command.Id, command.Request.DeviceId,
            command.Request.SimSlot, command.Request.HostToken, command.Request.LeaseToken, cancellationToken, allowCompleted: true);
        if (job.Status is SmsGatewayJobStatus.Sent or SmsGatewayJobStatus.Delivered or SmsGatewayJobStatus.Simulated)
            return Unit.Value;
        var sms = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new SmsSettings();
        if (SmsTestModePolicy.ShouldSimulate(sms, job.Phone))
        {
            job.Status = SmsGatewayJobStatus.Simulated;
            job.SimulatedAt = DateTime.UtcNow;
            SmsGatewayDeliverySync.Apply(job);
            await db.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
        var now = DateTime.UtcNow;
        SmsGatewayRoutingService.ResetQuota(device, now);
        job.Status = SmsGatewayJobStatus.Sent;
        job.SentAt = now;
        device.SentThisPeriod += job.SegmentCount;
        device.LastSentAt = now;
        device.LastError = null;
        if (job.CustomerId is long customerId)
        {
            var route = await db.CustomerSmsRoutes.FirstOrDefaultAsync(x => x.BranchId == job.BranchId
                && x.CustomerId == customerId, cancellationToken);
            if (route is null)
            {
                route = new CustomerSmsRoute
                {
                    BranchId = job.BranchId,
                    CustomerId = customerId,
                    LastDeviceId = device.Id,
                    LastSentAt = now
                };
                db.CustomerSmsRoutes.Add(route);
            }
            else
            {
                route.LastDeviceId = device.Id;
                route.LastSentAt = now;
            }
        }
        SmsGatewayDeliverySync.Apply(job);
        await db.SaveChangesAsync(cancellationToken);
        await quotaWarning.NotifyIfNeededAsync(device, cancellationToken);
        return Unit.Value;
    }
}

public sealed class MarkSmsGatewayJobDeliveredCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<MarkSmsGatewayJobDeliveredCommand, Unit>
{
    public async Task<Unit> Handle(MarkSmsGatewayJobDeliveredCommand command, CancellationToken cancellationToken)
    {
        var (job, _) = await SmsGatewayAccess.LoadLeaseAsync(db, currentUser, command.Id, command.Request.DeviceId,
            command.Request.SimSlot, command.Request.HostToken, command.Request.LeaseToken, cancellationToken, allowCompleted: true);
        if (job.Status == SmsGatewayJobStatus.Delivered)
            return Unit.Value;
        if (job.Status != SmsGatewayJobStatus.Sent)
            throw new BusinessRuleException("SMS hali yuborilmagan.");
        job.Status = SmsGatewayJobStatus.Delivered;
        job.DeliveredAt = DateTime.UtcNow;
        SmsGatewayDeliverySync.Apply(job);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class MarkSmsGatewayJobSimulatedCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<MarkSmsGatewayJobSimulatedCommand, Unit>
{
    public async Task<Unit> Handle(MarkSmsGatewayJobSimulatedCommand command, CancellationToken cancellationToken)
    {
        var (job, _) = await SmsGatewayAccess.LoadLeaseAsync(db, currentUser, command.Id, command.Request.DeviceId,
            command.Request.SimSlot, command.Request.HostToken, command.Request.LeaseToken, cancellationToken, allowCompleted: true);
        if (job.Status == SmsGatewayJobStatus.Simulated)
            return Unit.Value;
        if (job.Status != SmsGatewayJobStatus.Assigned)
            throw new BusinessRuleException("SMS ishi simulyatsiya uchun tayinlanmagan.");
        job.Status = SmsGatewayJobStatus.Simulated;
        job.SimulatedAt = DateTime.UtcNow;
        SmsGatewayDeliverySync.Apply(job);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class FailSmsGatewayJobCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<FailSmsGatewayJobCommand, Unit>
{
    public async Task<Unit> Handle(FailSmsGatewayJobCommand command, CancellationToken cancellationToken)
    {
        var lease = new SmsGatewayLeaseRequest(command.Request.DeviceId, command.Request.SimSlot,
            command.Request.HostToken, command.Request.LeaseToken);
        var (job, device) = await SmsGatewayAccess.LoadLeaseAsync(db, currentUser, command.Id, lease.DeviceId,
            lease.SimSlot, lease.HostToken, lease.LeaseToken, cancellationToken);
        job.Status = SmsGatewayJobStatus.Failed;
        job.ErrorCode = command.Request.ErrorCode;
        job.ErrorMessage = command.Request.ErrorMessage;
        device.LastError = command.Request.ErrorMessage;
        SmsGatewayDeliverySync.Apply(job);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class CreateSmsGatewayJobCommandHandler(
    SmsGatewayService service,
    ICurrentUser currentUser)
    : IRequestHandler<CreateSmsGatewayJobCommand, SmsGatewayJobDto?>
{
    public async Task<SmsGatewayJobDto?> Handle(CreateSmsGatewayJobCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        SmsGatewayAccess.EnsureBranch(currentUser, command.Request.BranchId);
        var kind = (SmsGatewayJobKind)command.Request.Kind;
        var job = await service.CreateAsync(command.Request.BranchId, kind, command.Request.Phone,
            command.Request.Text, SmsTextSegments.Count(command.Request.Text), command.Request.IdempotencyKey,
            command.Request.CustomerId, cancellationToken);
        return job is null ? null : SmsGatewayMapping.Job(job);
    }
}

public sealed class SendSmsGatewayTestCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    SmsGatewayService service)
    : IRequestHandler<SendSmsGatewayTestCommand, SmsGatewayJobDto?>
{
    public async Task<SmsGatewayJobDto?> Handle(SendSmsGatewayTestCommand command, CancellationToken cancellationToken)
    {
        var device = await db.SmsGatewayDevices.FirstOrDefaultAsync(x => x.DeviceId == command.Request.DeviceId
            && x.SimSlot == command.Request.SimSlot, cancellationToken)
            ?? throw new NotFoundException("SMS shlyuzi topilmadi.");
        if (currentUser.DeviceId != command.Request.DeviceId)
            throw new ForbiddenException("Qurilma identifikatori mos emas.");
        SmsGatewayCredential.Ensure(device, command.Request.HostToken);
        var job = await service.CreateAsync(device.BranchId, SmsGatewayJobKind.Manual, command.Request.Phone,
            "Cartex: test SMS", 1, $"sms-test:{device.Id}:{Guid.NewGuid():N}", null, cancellationToken);
        return job is null ? null : SmsGatewayMapping.Job(job);
    }
}

public sealed class UpdateSmsGatewayDeviceCommandValidator : AbstractValidator<UpdateSmsGatewayDeviceCommand>
{
    public UpdateSmsGatewayDeviceCommandValidator()
    {
        RuleFor(x => x.Request.Priority).InclusiveBetween(0, 10_000);
    }
}

public sealed class UpdateSmsGatewayConsentCommandValidator : AbstractValidator<UpdateSmsGatewayConsentCommand>
{
    public UpdateSmsGatewayConsentCommandValidator()
    {
        AddScopeRules(x => x.Request.Scope);
    }

    private void AddScopeRules(Func<UpdateSmsGatewayConsentCommand, SmsGatewayConsentScope> scope)
    {
        RuleFor(x => scope(x).MonthlyQuota).GreaterThan(0)
            .When(x => !scope(x).IsUnlimited);
        RuleFor(x => scope(x).MonthlyQuota).Null()
            .When(x => scope(x).IsUnlimited);
        RuleFor(x => scope(x).QuotaResetDay).InclusiveBetween(1, 28);
        RuleFor(x => scope(x).MaxPerHour).InclusiveBetween(1, 300);
        RuleFor(x => scope(x).MinIntervalSeconds).GreaterThanOrEqualTo(2);
        RuleFor(x => scope(x).LowQuotaWarnPercent).InclusiveBetween(1, 100);
    }
}

public sealed class RegisterSmsGatewayCommandValidator : AbstractValidator<RegisterSmsGatewayCommand>
{
    public RegisterSmsGatewayCommandValidator()
    {
        RuleFor(x => x.Request.DeviceId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.DeviceName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Client).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Request.SimSlot).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.SimOperator).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.SimSubscriptionId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.PhoneLabel).MaximumLength(30);
        RuleFor(x => x.Request.Consent).NotNull();
        RuleFor(x => x.Request.Consent!.MonthlyQuota).GreaterThan(0)
            .When(x => x.Request.Consent is { IsUnlimited: false });
        RuleFor(x => x.Request.Consent!.MonthlyQuota).Null()
            .When(x => x.Request.Consent is { IsUnlimited: true });
        RuleFor(x => x.Request.Consent!.QuotaResetDay).InclusiveBetween(1, 28)
            .When(x => x.Request.Consent is not null);
        RuleFor(x => x.Request.Consent!.MaxPerHour).InclusiveBetween(1, 300)
            .When(x => x.Request.Consent is not null);
        RuleFor(x => x.Request.Consent!.MinIntervalSeconds).GreaterThanOrEqualTo(2)
            .When(x => x.Request.Consent is not null);
        RuleFor(x => x.Request.Consent!.LowQuotaWarnPercent).InclusiveBetween(1, 100)
            .When(x => x.Request.Consent is not null);
    }
}

public sealed class SendSmsGatewayTestCommandValidator : AbstractValidator<SendSmsGatewayTestCommand>
{
    public SendSmsGatewayTestCommandValidator()
    {
        RuleFor(x => x.Request.Phone).NotEmpty().MaximumLength(30);
    }
}

internal static class SmsGatewayAccess
{
    public static void EnsureManage(ICurrentUser user)
    {
        if (!user.HasPermission(AppPermissions.SmsGateway.Edit))
            throw new ForbiddenException("SMS shlyuzini boshqarish ruxsati kerak.");
    }

    public static void EnsureHost(ICurrentUser user)
    {
        if (!user.HasPermission(AppPermissions.SmsGateway.Host))
            throw new ForbiddenException("SMS shlyuzi host ruxsati kerak.");
    }

    public static void EnsureBranch(ICurrentUser user, long branchId)
    {
        if (!user.CanAccessAllBranches && !user.BranchIds.Contains(branchId))
            throw new ForbiddenException("Filialga ruxsat yo'q.");
    }

    public static async Task<List<SmsGatewayJob>> DetachJobsAsync(IApplicationDbContext db, long deviceId, CancellationToken cancellationToken)
    {
        var jobs = await db.SmsGatewayJobs.Where(x => x.AssignedDeviceId == deviceId && x.Status == SmsGatewayJobStatus.Assigned)
            .ToListAsync(cancellationToken);
        foreach (var job in jobs)
            SmsGatewayService.ClearAssignment(job);
        return jobs;
    }

    public static async Task<SmsGatewayDevice> LoadOwnedDeviceAsync(
        IApplicationDbContext db,
        ICurrentUser user,
        long id,
        string deviceId,
        int simSlot,
        string hostToken,
        CancellationToken cancellationToken)
    {
        EnsureHost(user);
        var device = await db.SmsGatewayDevices.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException("SMS shlyuzi topilmadi.");
        if (device.DeviceId != deviceId || device.SimSlot != simSlot || user.DeviceId != deviceId)
            throw new ForbiddenException("Qurilma identifikatori mos emas.");
        SmsGatewayCredential.Ensure(device, hostToken);
        return device;
    }

    public static async Task<(SmsGatewayJob Job, SmsGatewayDevice Device)> LoadLeaseAsync(
        IApplicationDbContext db, ICurrentUser user, long jobId, string deviceId, int simSlot, string hostToken,
        string leaseToken, CancellationToken cancellationToken, bool allowCompleted = false)
    {
        EnsureHost(user);
        if (user.DeviceId != deviceId)
            throw new ForbiddenException("Qurilma identifikatori mos emas.");
        var job = await db.SmsGatewayJobs.Include(x => x.AssignedDevice)
            .Include(x => x.NotificationDelivery).Include(x => x.NotificationDeliveryAttempt)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken)
            ?? throw new NotFoundException("SMS ishi topilmadi.");
        var device = job.AssignedDevice ?? throw new ForbiddenException("SMS ishi qurilmaga tayinlanmagan.");
        SmsGatewayCredential.Ensure(device, hostToken);
        if (job.LeaseToken != leaseToken || device.DeviceId != deviceId || device.SimSlot != simSlot)
            throw new ForbiddenException("SMS lease noto'g'ri.");
        if (!allowCompleted && job.Status != SmsGatewayJobStatus.Assigned)
            throw new BusinessRuleException("SMS ishi yuborish uchun tayinlanmagan.");
        if (job.Status == SmsGatewayJobStatus.Assigned && job.LeaseExpiresAt <= DateTime.UtcNow)
            throw new BusinessRuleException("SMS lease muddati o'tgan.");
        if (!device.IsTrusted || !device.IsConsented || !device.IsEnabled || device.PausedAt is not null)
            throw new ForbiddenException("SMS shlyuzi faol emas.");
        return (job, device);
    }
}

internal static class SmsGatewayMapping
{
    public static SmsGatewayDeviceDto Device(SmsGatewayDevice x, int sentLastHour = 0, int linkedCustomers = 0) => new(x.Id, x.BranchId, x.DeviceId,
        x.DeviceName, x.Client, x.SimSlot, x.SimOperator, x.SimSubscriptionId, x.PhoneLabel,
        x.IsTrusted, x.IsConsented, x.IsEnabled, x.PausedAt is not null, x.MonthlyQuota, x.QuotaResetDay, x.SentThisPeriod,
        x.MaxPerHour, x.MinIntervalSeconds, x.Priority, x.LastSeenAt, x.LastError,
        x.LastSeenAt >= DateTime.UtcNow.AddSeconds(-90), SmsGatewayState.BlockReason(x, sentLastHour, DateTime.UtcNow),
        SmsGatewayState.QuotaResetsAt(x, DateTime.UtcNow), x.LowQuotaWarnPercent,
        x.MonthlyQuota is int quota && x.SentThisPeriod > quota, x.LastSentAt, linkedCustomers);

    public static SmsGatewayJobDto Job(SmsGatewayJob x, bool sensitive = false) => new(x.Id, x.BranchId, (SharedKind)x.Kind,
        MaskPhone(x.Phone), sensitive ? x.Phone : null, x.Text, x.CustomerId, x.Customer?.Party.FullName,
        (SharedStatus)x.Status, x.AssignedDeviceId,
        x.AssignedDevice is null ? null : $"{x.AssignedDevice.PhoneLabel ?? x.AssignedDevice.SimOperator} · SIM {x.AssignedDevice.SimSlot + 1}",
        x.AssignedDevice?.SimSlot, x.SegmentCount, x.AttemptCount, x.ErrorCode, x.ErrorMessage,
        x.WaitingReason, x.AvailableAt, x.NotificationDeliveryId, x.RetryOfJobId, x.CreatedAt, x.SentAt, x.DeliveredAt);

    private static string MaskPhone(string phone) => phone.Length <= 4 ? "****" : $"***{phone[^4..]}";
}

public static class SmsTextSegments
{
    private static readonly HashSet<char> GsmBasic =
        [.. "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà"];
    private static readonly HashSet<char> GsmExtended = [.. "^{}\\[~]|€"];

    public static int Count(string text)
    {
        var septets = 0;
        foreach (var character in text)
        {
            if (GsmBasic.Contains(character))
                septets++;
            else if (GsmExtended.Contains(character))
                septets += 2;
            else
                return text.Length <= 70 ? 1 : (int)Math.Ceiling((double)text.Length / 67);
        }
        return septets <= 160 ? 1 : (int)Math.Ceiling((double)septets / 153);
    }
}

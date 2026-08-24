using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.SmsGateway;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;
using SmsGatewayJobKind = Cartex.Domain.Enums.SmsGatewayJobKind;
using SmsGatewayJobStatus = Cartex.Domain.Enums.SmsGatewayJobStatus;

namespace Cartex.Application.Sms;

public record GetSmsGatewayJournalQuery(
    long BranchId,
    string? Status = null,
    string? Kind = null,
    long? DeviceId = null,
    long? CustomerId = null,
    DateTime? From = null,
    DateTime? To = null,
    int Take = 100) : IRequest<SmsGatewayJournalDto>;

public record RetrySmsGatewayJobsCommand(IReadOnlyList<long> JobIds) : ICommand<Unit>;
public record CancelSmsGatewayJobsCommand(IReadOnlyList<long> JobIds) : ICommand<Unit>;
public record ReassignSmsGatewayJobsCommand(IReadOnlyList<long> JobIds, long DeviceId) : ICommand<Unit>;

public sealed class GetSmsGatewayJournalQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSmsGatewayJournalQuery, SmsGatewayJournalDto>
{
    public async Task<SmsGatewayJournalDto> Handle(GetSmsGatewayJournalQuery request, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        SmsGatewayAccess.EnsureBranch(currentUser, request.BranchId);
        var baseQuery = db.SmsGatewayJobs.AsNoTracking().Where(x => x.BranchId == request.BranchId);
        var filtered = Apply(baseQuery, request);
        var sensitive = currentUser.HasPermission(AppPermissions.Notifications.JournalSensitive);
        var jobs = await filtered.Include(x => x.Customer).Include(x => x.AssignedDevice)
            .OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(request.Take, 1, 500))
            .ToListAsync(cancellationToken);
        var localToday = DateTime.Today;
        var today = localToday.ToUniversalTime();
        var month = new DateTime(localToday.Year, localToday.Month, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var counts = await baseQuery.Where(x => x.CreatedAt >= month)
            .GroupBy(x => new { Today = x.CreatedAt >= today, x.Status })
            .Select(x => new { x.Key.Today, x.Key.Status, Count = x.Count() })
            .ToListAsync(cancellationToken);
        SmsGatewayJournalPeriodDto Period(bool todayOnly)
        {
            var rows = todayOnly ? counts.Where(x => x.Today) : counts;
            return new SmsGatewayJournalPeriodDto(
                rows.Where(x => x.Status is SmsGatewayJobStatus.Sent or SmsGatewayJobStatus.Delivered).Sum(x => x.Count),
                rows.Where(x => x.Status is SmsGatewayJobStatus.Pending or SmsGatewayJobStatus.Assigned).Sum(x => x.Count),
                rows.Where(x => x.Status is SmsGatewayJobStatus.Failed or SmsGatewayJobStatus.Rejected).Sum(x => x.Count));
        }
        return new SmsGatewayJournalDto(jobs.Select(x => SmsGatewayMapping.Job(x, sensitive)).ToList(),
            new SmsGatewayJournalSummaryDto(Period(true), Period(false)));
    }

    private static IQueryable<SmsGatewayJob> Apply(IQueryable<SmsGatewayJob> query, GetSmsGatewayJournalQuery request)
    {
        if (Enum.TryParse<SmsGatewayJobStatus>(request.Status, true, out var status))
            query = query.Where(x => x.Status == status);
        if (Enum.TryParse<SmsGatewayJobKind>(request.Kind, true, out var kind))
            query = query.Where(x => x.Kind == kind);
        if (request.DeviceId is long deviceId)
            query = query.Where(x => x.AssignedDeviceId == deviceId);
        if (request.CustomerId is long customerId)
            query = query.Where(x => x.CustomerId == customerId);
        if (request.From is DateTime from)
            query = query.Where(x => x.CreatedAt >= NormalizeUtc(from));
        if (request.To is DateTime to)
            query = query.Where(x => x.CreatedAt < NormalizeUtc(to));
        return query;
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed class RetrySmsGatewayJobsCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    SmsGatewayService service,
    IAuditService audit) : IRequestHandler<RetrySmsGatewayJobsCommand, Unit>
{
    public async Task<Unit> Handle(RetrySmsGatewayJobsCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        var jobs = await LoadAsync(db, command.JobIds, cancellationToken);
        foreach (var job in jobs)
        {
            SmsGatewayAccess.EnsureBranch(currentUser, job.BranchId);
            if (job.Status is not (SmsGatewayJobStatus.Failed or SmsGatewayJobStatus.Rejected or SmsGatewayJobStatus.Cancelled))
                throw new BusinessRuleException("Faqat yopilgan SMS ishini qayta urinish mumkin.");
            if (job.NotificationDelivery is null)
                throw new BusinessRuleException("Eski SMS ishida xabarnoma bog'lanishi yo'q.");
            var attempt = new NotificationDeliveryAttempt
            {
                NotificationDelivery = job.NotificationDelivery,
                AttemptNumber = job.NotificationDelivery.Attempts.Max(x => x.AttemptNumber) + 1,
                Provider = "device",
                Units = job.SegmentCount
            };
            job.NotificationDelivery.Attempts.Add(attempt);
            job.NotificationDelivery.Status = NotificationDeliveryStatus.Pending;
            job.NotificationDelivery.CompletedAt = null;
            await db.SaveChangesAsync(cancellationToken);
            var retry = await service.CreateAsync(job.BranchId, job.Kind, job.Phone, job.Text, job.SegmentCount,
                $"{job.IdempotencyKey}:retry:{Guid.NewGuid():N}", job.CustomerId, cancellationToken,
                job.NotificationDelivery.Id, attempt.Id, job.Id);
            audit.SetOutcome("sms.gateway_job_retried", "sms_gateway_jobs", retry?.Id,
                new { sourceJobId = job.Id, retryJobId = retry?.Id }, branchId: job.BranchId);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private static Task<List<SmsGatewayJob>> LoadAsync(
        IApplicationDbContext db,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken) => db.SmsGatewayJobs
        .Include(x => x.NotificationDelivery!).ThenInclude(x => x.Attempts)
        .Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
}

public sealed class CancelSmsGatewayJobsCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<CancelSmsGatewayJobsCommand, Unit>
{
    public async Task<Unit> Handle(CancelSmsGatewayJobsCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        var jobs = await db.SmsGatewayJobs.Include(x => x.NotificationDelivery).Include(x => x.NotificationDeliveryAttempt)
            .Where(x => command.JobIds.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            SmsGatewayAccess.EnsureBranch(currentUser, job.BranchId);
            if (job.Status is not (SmsGatewayJobStatus.Pending or SmsGatewayJobStatus.Assigned))
                throw new BusinessRuleException("Faqat kutayotgan SMS ishini bekor qilish mumkin.");
            job.Status = SmsGatewayJobStatus.Cancelled;
            job.LeaseToken = null;
            job.LeaseExpiresAt = null;
            job.WaitingReason = null;
            job.AvailableAt = null;
            SmsGatewayDeliverySync.Apply(job);
            audit.SetOutcome("sms.gateway_job_cancelled", "sms_gateway_jobs", job.Id,
                new { job.Id }, branchId: job.BranchId);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class ReassignSmsGatewayJobsCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    SmsGatewayRoutingService routing,
    IAuditService audit) : IRequestHandler<ReassignSmsGatewayJobsCommand, Unit>
{
    public async Task<Unit> Handle(ReassignSmsGatewayJobsCommand command, CancellationToken cancellationToken)
    {
        SmsGatewayAccess.EnsureManage(currentUser);
        var jobs = await db.SmsGatewayJobs.Include(x => x.NotificationDelivery).Include(x => x.NotificationDeliveryAttempt)
            .Where(x => command.JobIds.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            SmsGatewayAccess.EnsureBranch(currentUser, job.BranchId);
            if (job.Status is not (SmsGatewayJobStatus.Pending or SmsGatewayJobStatus.Assigned))
                throw new BusinessRuleException("Faqat kutayotgan SMS ishini o'tkazish mumkin.");
            SmsGatewayService.ClearAssignment(job);
            if (!await routing.AssignToAsync(job, command.DeviceId, cancellationToken))
                throw new BusinessRuleException("Tanlangan SIM hozir yubora olmaydi.");
            audit.SetOutcome("sms.gateway_job_reassigned", "sms_gateway_jobs", job.Id,
                new { job.Id, command.DeviceId }, branchId: job.BranchId);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class SmsGatewayJobIdsValidator : AbstractValidator<RetrySmsGatewayJobsCommand>
{
    public SmsGatewayJobIdsValidator()
    {
        RuleFor(x => x.JobIds).NotEmpty().Must(x => x.Distinct().Count() <= 100);
    }
}

public sealed class CancelSmsGatewayJobsCommandValidator : AbstractValidator<CancelSmsGatewayJobsCommand>
{
    public CancelSmsGatewayJobsCommandValidator()
    {
        RuleFor(x => x.JobIds).NotEmpty().Must(x => x.Distinct().Count() <= 100);
    }
}

public sealed class ReassignSmsGatewayJobsCommandValidator : AbstractValidator<ReassignSmsGatewayJobsCommand>
{
    public ReassignSmsGatewayJobsCommandValidator()
    {
        RuleFor(x => x.JobIds).NotEmpty().Must(x => x.Distinct().Count() <= 100);
        RuleFor(x => x.DeviceId).GreaterThan(0);
    }
}

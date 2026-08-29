using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Printing;
using Microsoft.EntityFrameworkCore;
using DomainJobKind = Cartex.Domain.Enums.PrintJobKind;
using DomainJobStatus = Cartex.Domain.Enums.PrintJobStatus;
using DomainAttemptStatus = Cartex.Domain.Enums.PrintAttemptStatus;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Printing;

public record GetPrintNodesQuery(long BranchId) : IRequest<IReadOnlyList<PrintNodeDto>>;

public sealed class GetPrintNodesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, IHubPresence presence)
    : IRequestHandler<GetPrintNodesQuery, IReadOnlyList<PrintNodeDto>>
{
    public async Task<IReadOnlyList<PrintNodeDto>> Handle(GetPrintNodesQuery request, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        var nodes = await db.PrintNodes.AsNoTracking().Include(x => x.Endpoints)
            .Where(x => x.BranchId == request.BranchId)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return nodes.Select(x => PrintingMapper.Node(x, presence.IsOnline(HubChannels.PrintHost(x.DeviceId)))).ToList();
    }
}

public record GetPrintRequesterDevicesQuery(long BranchId) : IRequest<IReadOnlyList<PrintRequesterDeviceDto>>;

public sealed class GetPrintRequesterDevicesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPrintRequesterDevicesQuery, IReadOnlyList<PrintRequesterDeviceDto>>
{
    public async Task<IReadOnlyList<PrintRequesterDeviceDto>> Handle(GetPrintRequesterDevicesQuery request, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        return await db.PrintRequesterDevices.AsNoTracking()
            .Where(x => x.BranchId == request.BranchId)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => new PrintRequesterDeviceDto(x.Id, x.BranchId, x.DeviceId, x.Name, x.Client,
                x.IsTrusted, x.FirstSeenAt, x.LastSeenAt, x.LastUser == null ? null : x.LastUser.Username))
            .ToListAsync(cancellationToken);
    }
}

public record GetPrintDevicesQuery(long BranchId) : IRequest<PrintDevicesDto>;

/// One row per physical device for the settings screen: requester record and host node
/// joined by device id, so trust reads as a single switch.
public sealed class GetPrintDevicesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, IHubPresence presence)
    : IRequestHandler<GetPrintDevicesQuery, PrintDevicesDto>
{
    public async Task<PrintDevicesDto> Handle(GetPrintDevicesQuery request, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        var autoTrust = await db.Branches.Where(x => x.Id == request.BranchId)
            .Select(x => x.AutoTrustPrintDevices).FirstOrDefaultAsync(cancellationToken);
        var nodes = await db.PrintNodes.AsNoTracking().Include(x => x.Endpoints)
            .Where(x => x.BranchId == request.BranchId).ToListAsync(cancellationToken);
        var requesters = await db.PrintRequesterDevices.AsNoTracking().Include(x => x.LastUser)
            .Where(x => x.BranchId == request.BranchId).ToListAsync(cancellationToken);

        var devices = nodes.Select(x => x.DeviceId).Union(requesters.Select(x => x.DeviceId), StringComparer.Ordinal)
            .Select(deviceId =>
            {
                var node = nodes.FirstOrDefault(x => x.DeviceId == deviceId);
                var requester = requesters.FirstOrDefault(x => x.DeviceId == deviceId);
                var status = node is null
                    ? (Cartex.Shared.Models.Printing.PrintNodeStatus?)null
                    : presence.IsOnline(HubChannels.PrintHost(deviceId))
                        ? Cartex.Shared.Models.Printing.PrintNodeStatus.Online
                        : Cartex.Shared.Models.Printing.PrintNodeStatus.Offline;
                var lastSeen = new[] { node?.LastSeenAt, requester?.LastSeenAt }.Max();
                return new PrintDeviceDto(
                    deviceId,
                    node?.Name ?? requester!.Name,
                    requester?.Client ?? node?.LastClient,
                    (node?.IsTrusted ?? false) || (requester?.IsTrusted ?? false),
                    lastSeen,
                    requester?.LastUser?.Username,
                    node?.Id,
                    status,
                    node?.HostEnabled ?? false,
                    node?.Endpoints.OrderBy(x => x.DisplayName).Select(PrintingMapper.Endpoint).ToList() ?? []);
            })
            .OrderByDescending(x => x.LastSeenAt)
            .ToList();
        return new PrintDevicesDto(autoTrust, devices);
    }
}

public record GetPrintRoutingPoliciesQuery(long BranchId) : IRequest<IReadOnlyList<PrintRoutingPolicyDto>>;

public sealed class GetPrintRoutingPoliciesQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    PrintRoutingService routing) : IRequestHandler<GetPrintRoutingPoliciesQuery, IReadOnlyList<PrintRoutingPolicyDto>>
{
    public async Task<IReadOnlyList<PrintRoutingPolicyDto>> Handle(GetPrintRoutingPoliciesQuery request, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        var result = new List<PrintRoutingPolicyDto>();
        foreach (var kind in Enum.GetValues<DomainJobKind>())
        {
            var policy = await routing.GetOrCreatePolicyAsync(request.BranchId, kind, cancellationToken);
            result.Add(await PrintingMapper.PolicyAsync(db, policy.Id, cancellationToken));
        }
        return result;
    }
}

public record GetPrintingBootstrapQuery(long BranchId, string? DeviceId)
    : IRequest<PrintingBootstrapDto>;

public sealed class GetPrintingBootstrapQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    PrintRoutingService routing,
    ReceiptPrintPolicyService receiptPolicy,
    IHubPresence presence)
    : IRequestHandler<GetPrintingBootstrapQuery, PrintingBootstrapDto>
{
    public async Task<PrintingBootstrapDto> Handle(
        GetPrintingBootstrapQuery request,
        CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        var policy = await routing.GetOrCreatePolicyAsync(request.BranchId, DomainJobKind.Receipt, cancellationToken);
        var (business, effective) = await receiptPolicy.ResolveAsync(policy, cancellationToken);

        PrintNodeDto? node = null;
        var deviceId = request.DeviceId?.Trim();
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var entity = await db.PrintNodes.AsNoTracking().Include(x => x.Endpoints)
                .FirstOrDefaultAsync(x => x.BranchId == request.BranchId && x.DeviceId == deviceId,
                    cancellationToken);
            if (entity is not null) node = PrintingMapper.Node(entity, presence.IsOnline(HubChannels.PrintHost(entity.DeviceId)));
        }

        var settingRevision = await db.BusinessSettings.AsNoTracking()
            .Where(x => x.Key == SettingKeys.Receipt)
            .Select(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var revision = $"{settingRevision.Ticks:x}-{policy.Revision:x}";
        return new PrintingBootstrapDto(
            request.BranchId,
            deviceId,
            ReceiptPrintPolicyService.ToDto(business),
            ReceiptPrintPolicyService.ToDto(effective),
            await PrintingMapper.PolicyAsync(db, policy.Id, cancellationToken),
            node,
            revision,
            DateTime.UtcNow);
    }
}

public record GetPrintJobReceiptSettingsQuery(long JobId, string ReceiptToken)
    : IRequest<ReceiptSettings?>;

public sealed class GetPrintJobReceiptSettingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPrintJobReceiptSettingsQuery, ReceiptSettings?>
{
    public async Task<ReceiptSettings?> Handle(
        GetPrintJobReceiptSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var payloadJson = await db.PrintJobs.AsNoTracking()
            .Where(x => x.Id == request.JobId && x.Kind == DomainJobKind.Receipt)
            .Select(x => x.PayloadJson)
            .FirstOrDefaultAsync(cancellationToken);
        if (payloadJson is null) return null;
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("receiptToken", out var token)
                || token.GetString() != request.ReceiptToken
                || !root.TryGetProperty("receiptSettings", out var value))
                return null;
            var result = JsonSerializer.Deserialize<ReceiptSettings>(value.GetRawText(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (result is not null && value.TryGetProperty("publicReceiptBaseUrl", out var publicUrl))
                result.PublicReceiptBaseUrl = publicUrl.GetString();
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public record GetAssignedPrintJobsQuery(string DeviceId, string HostToken) : IRequest<IReadOnlyList<AssignedPrintJobDto>>;

public sealed class GetAssignedPrintJobsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetAssignedPrintJobsQuery, IReadOnlyList<AssignedPrintJobDto>>
{
    public async Task<IReadOnlyList<AssignedPrintJobDto>> Handle(GetAssignedPrintJobsQuery request, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureDevice(currentUser, request.DeviceId);
        var node = await db.PrintNodes.FirstOrDefaultAsync(x => x.DeviceId == request.DeviceId, cancellationToken)
            ?? throw new NotFoundException("Print node not found.");
        PrintingCredential.Ensure(node, request.HostToken);
        var jobs = await db.PrintJobs.AsNoTracking()
            .Include(x => x.AssignedNode).Include(x => x.AssignedEndpoint)
            .Where(x => x.AssignedNode!.DeviceId == request.DeviceId
                && (x.Status == DomainJobStatus.Assigned || x.Status == DomainJobStatus.Accepted
                    || x.Status == DomainJobStatus.SpoolSubmitted))
            .OrderBy(x => x.CreatedAt).Take(20).ToListAsync(cancellationToken);
        foreach (var job in jobs) PrintingGuard.EnsureBranch(currentUser, job.BranchId);
        return jobs.Select(job =>
        {
            using var document = JsonDocument.Parse(job.PayloadJson);
            return new AssignedPrintJobDto(job.Id, job.BranchId,
                (Cartex.Shared.Models.Printing.PrintJobKind)job.Kind, job.SourceType, job.SourceId,
                document.RootElement.Clone(), job.Copies, job.IsReprint, job.LeaseToken!,
                job.LeaseExpiresAt ?? DateTime.UtcNow, job.AssignedEndpointId!.Value,
                job.AssignedEndpoint!.SystemName, job.AssignedEndpoint.DisplayName,
                job.AssignedEndpoint.ProfileJson);
        }).ToList();
    }
}

public record GetPrintJobsQuery(long BranchId, int Take = 100) : IRequest<IReadOnlyList<PrintJobDto>>;

public sealed class GetPrintJobsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPrintJobsQuery, IReadOnlyList<PrintJobDto>>
{
    public async Task<IReadOnlyList<PrintJobDto>> Handle(GetPrintJobsQuery request, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        var query = db.PrintJobs.AsNoTracking().Where(x => x.BranchId == request.BranchId);
        if (!currentUser.HasPermission(AppPermissions.Printing.JobsViewBranch))
        {
            if (!currentUser.HasPermission(AppPermissions.Printing.JobsViewOwn))
                throw new ForbiddenException("Print job access denied.");
            query = query.Where(x => x.RequestedByUserId == currentUser.UserId);
        }
        var jobs = await query
            .Include(x => x.RequestedByUser)
            .Include(x => x.AssignedNode)
            .Include(x => x.AssignedEndpoint)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(request.Take, 1, 500))
            .ToListAsync(cancellationToken);
        return jobs.Select(PrintingMapper.Job).ToList();
    }
}

public record CancelPrintJobCommand(long Id) : ICommand<Unit>;

public sealed class CancelPrintJobCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CancelPrintJobCommand, Unit>
{
    public async Task<Unit> Handle(CancelPrintJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.PrintJobs.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Print job not found.");
        PrintingGuard.EnsureBranch(currentUser, job.BranchId);
        if (job.Status is not (DomainJobStatus.Pending or DomainJobStatus.Assigned))
            throw new BusinessRuleException("Only pending or assigned print jobs can be cancelled.");
        job.Status = DomainJobStatus.Cancelled;
        job.CancelledAt = DateTime.UtcNow;
        job.LeaseToken = null;
        job.LeaseExpiresAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record RetryPrintJobCommand(long Id) : ICommand<PrintJobDto>;

public sealed class RetryPrintJobCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    PrintRoutingService routing,
    IPrintJobNotifier notifier) : IRequestHandler<RetryPrintJobCommand, PrintJobDto>
{
    public async Task<PrintJobDto> Handle(RetryPrintJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.PrintJobs.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Print job not found.");
        PrintingGuard.EnsureBranch(currentUser, job.BranchId);
        if (job.Status == DomainJobStatus.Completed)
            throw new BusinessRuleException("Create an audited reprint instead of retrying a completed job.");
        if (job.Status == DomainJobStatus.ManualReview)
            throw new BusinessRuleException("Resolve the unknown spool result before retrying.");
        job.Status = DomainJobStatus.Pending;
        job.AssignedNodeId = null;
        job.AssignedEndpointId = null;
        job.LeaseToken = null;
        job.LeaseExpiresAt = null;
        await db.SaveChangesAsync(cancellationToken);
        if (await routing.AssignAsync(job, cancellationToken) && job.AssignedNodeId is not null)
        {
            var deviceId = await db.PrintNodes.Where(x => x.Id == job.AssignedNodeId).Select(x => x.DeviceId).FirstAsync(cancellationToken);
            await db.RunAfterCommitAsync(() => notifier.NotifyJobAvailableAsync(deviceId, job.Id, cancellationToken));
        }
        return PrintingMapper.Job(job);
    }
}

public record RecoverPrintJobsCommand : ICommand<int>;

public sealed class RecoverPrintJobsCommandHandler(
    IApplicationDbContext db,
    PrintRoutingService routing,
    IPrintJobNotifier notifier,
    IHubPresence presence) : IRequestHandler<RecoverPrintJobsCommand, int>
{
    public async Task<int> Handle(RecoverPrintJobsCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var inFlight = await db.PrintJobs.Include(x => x.Attempts).Include(x => x.AssignedNode)
            .Where(x => x.Status == DomainJobStatus.Assigned || x.Status == DomainJobStatus.Accepted)
            .OrderBy(x => x.CreatedAt)
            .Take(100).ToListAsync(cancellationToken);
        var expired = inFlight
            .Select(job => (Job: job, Reason: job.LeaseExpiresAt < now
                ? "lease_expired"
                : job.Status == DomainJobStatus.Assigned && !presence.IsOnline(HubChannels.PrintHost(job.AssignedNode?.DeviceId ?? ""))
                    ? "host_offline"
                    : null))
            .Where(x => x.Reason is not null)
            .ToList();
        foreach (var (job, reason) in expired)
        {
            var attempt = job.Attempts.FirstOrDefault(x => x.LeaseToken == job.LeaseToken);
            if (attempt is not null)
            {
                attempt.Status = DomainAttemptStatus.FailedBeforeSubmit;
                attempt.CompletedAt = now;
                attempt.ErrorCode = reason;
            }
            job.Status = DomainJobStatus.Pending;
            job.AssignedNodeId = null;
            job.AssignedEndpointId = null;
            job.LeaseToken = null;
            job.LeaseExpiresAt = null;
        }
        if (expired.Count > 0) await db.SaveChangesAsync(cancellationToken);

        var uncertain = await db.PrintJobs.Include(x => x.Attempts)
            .Where(x => x.Status == DomainJobStatus.SpoolSubmitted && x.SubmittedAt < now.AddMinutes(-10))
            .OrderBy(x => x.CreatedAt)
            .Take(100).ToListAsync(cancellationToken);
        foreach (var job in uncertain)
        {
            job.Status = DomainJobStatus.ManualReview;
            var attempt = job.Attempts.FirstOrDefault(x => x.LeaseToken == job.LeaseToken);
            if (attempt is not null)
            {
                attempt.Status = DomainAttemptStatus.UnknownAfterSubmit;
                attempt.ErrorCode = "spool_confirmation_timeout";
            }
        }
        if (uncertain.Count > 0) await db.SaveChangesAsync(cancellationToken);

        var pending = await db.PrintJobs.Where(x => x.Status == DomainJobStatus.Pending)
            .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        var assigned = 0;
        foreach (var job in pending)
        {
            if (!await routing.AssignAsync(job, cancellationToken) || job.AssignedNodeId is null) continue;
            var deviceId = await db.PrintNodes.Where(x => x.Id == job.AssignedNodeId).Select(x => x.DeviceId).FirstAsync(cancellationToken);
            var jobId = job.Id;
            await db.RunAfterCommitAsync(() => notifier.NotifyJobAvailableAsync(deviceId, jobId, cancellationToken));
            assigned++;
        }
        return assigned;
    }
}

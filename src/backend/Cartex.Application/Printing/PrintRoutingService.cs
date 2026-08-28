using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Printing;

public sealed class PrintRoutingService(IApplicationDbContext db, IHubPresence presence)
{
    public async Task<PrintRoutingPolicy> GetOrCreatePolicyAsync(long branchId, PrintJobKind kind, CancellationToken cancellationToken)
    {
        var policy = await db.PrintRoutingPolicies
            .Include(x => x.Targets)
            .ThenInclude(x => x.PrinterEndpoint)
            .ThenInclude(x => x.PrintNode)
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Kind == kind, cancellationToken);

        if (policy is not null) return policy;

        policy = new PrintRoutingPolicy
        {
            BranchId = branchId,
            Kind = kind,
            IsEnabled = true
        };
        db.PrintRoutingPolicies.Add(policy);
        await db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task<bool> AssignAsync(PrintJob job, CancellationToken cancellationToken)
    {
        var policy = await GetOrCreatePolicyAsync(job.BranchId, job.Kind, cancellationToken);
        if (!policy.IsEnabled) return false;

        var capability = CapabilityFor(job.Kind);
        var endpoints = await db.PrinterEndpoints
            .Include(x => x.PrintNode)
            .Where(x => x.PrintNode.BranchId == job.BranchId
                && x.IsEnabled
                && x.PrintNode.IsTrusted
                && x.PrintNode.HostEnabled
                && (x.Capabilities & capability) == capability)
            .ToListAsync(cancellationToken);
        endpoints.RemoveAll(x => !presence.IsOnline(HubChannels.PrintHost(x.PrintNode.DeviceId)));

        var attempted = await db.PrintAttempts
            .Where(x => x.PrintJobId == job.Id && x.ErrorCode != "host_offline")
            .Select(x => new { x.PrinterEndpointId, FailedAt = x.CompletedAt ?? x.StartedAt })
            .ToListAsync(cancellationToken);
        endpoints.RemoveAll(endpoint => attempted.Any(attempt => attempt.PrinterEndpointId == endpoint.Id
            && (endpoint.LastSeenAt ?? DateTime.MinValue) <= attempt.FailedAt));

        // A live printer is preferred, but a printer the spooler reports as away is still a
        // valid last resort: Windows queues the job and a sleeping network printer wakes on
        // the first byte. Refusing it would freeze the queue on a machine that can print.
        var live = endpoints
            .Where(x => x.Status is PrinterEndpointStatus.Ready or PrinterEndpointStatus.Busy)
            .ToList();
        var endpoint = SelectEndpoint(job, policy, live.Count > 0 ? live : endpoints);
        if (endpoint is null) return false;

        var now = DateTime.UtcNow;
        var leaseToken = Guid.NewGuid().ToString("N");
        job.Status = PrintJobStatus.Assigned;
        job.AssignedNodeId = endpoint.PrintNodeId;
        job.AssignedEndpointId = endpoint.Id;
        job.LeaseToken = leaseToken;
        job.LeaseExpiresAt = now.AddSeconds(policy.AssignmentTimeoutSeconds);
        job.AssignedAt = now;
        job.AttemptCount++;
        job.ErrorCode = null;
        job.ErrorMessage = null;
        db.PrintAttempts.Add(new PrintAttempt
        {
            PrintJobId = job.Id,
            AttemptNumber = job.AttemptCount,
            PrintNodeId = endpoint.PrintNodeId,
            PrinterEndpointId = endpoint.Id,
            LeaseToken = leaseToken
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static PrintCapability CapabilityFor(PrintJobKind kind) => kind switch
    {
        PrintJobKind.Receipt => PrintCapability.Receipt,
        PrintJobKind.BarcodeLabel => PrintCapability.BarcodeLabel,
        PrintJobKind.ZReport => PrintCapability.ZReport,
        PrintJobKind.Document => PrintCapability.Document,
        PrintJobKind.CartProforma => PrintCapability.CartProforma,
        _ => PrintCapability.None
    };

    private static PrinterEndpoint? SelectEndpoint(
        PrintJob job,
        PrintRoutingPolicy policy,
        IReadOnlyCollection<PrinterEndpoint> endpoints)
    {
        if (endpoints.Count == 0) return null;

        if (policy.RoutingMode == PrintRoutingMode.LocalOnly)
            return Configured(policy, endpoints);

        if (policy.RoutingMode == PrintRoutingMode.LocalFirst)
        {
            var local = job.OriginNodeId is null
                ? null
                : endpoints.FirstOrDefault(x => x.PrintNodeId == job.OriginNodeId);
            if (local is not null) return local;
        }

        if (IsStickyValid(policy))
        {
            var sticky = endpoints.FirstOrDefault(x => x.Id == policy.StickyEndpointId);
            if (sticky is not null) return sticky;
        }

        return Configured(policy, endpoints)
            ?? (policy.AllowFallback
                ? endpoints.OrderByDescending(x => x.LastSuccessAt).ThenBy(x => x.Id).FirstOrDefault()
                : null);
    }

    private static PrinterEndpoint? Configured(PrintRoutingPolicy policy, IReadOnlyCollection<PrinterEndpoint> endpoints) =>
        policy.Targets
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Priority)
            .Select(x => endpoints.FirstOrDefault(e => e.Id == x.PrinterEndpointId))
            .FirstOrDefault(x => x is not null);

    private static bool IsStickyValid(PrintRoutingPolicy policy) => policy.StickyEndpointId is not null && policy.StickyMode switch
    {
        PrintStickyMode.Disabled => false,
        PrintStickyMode.Duration => policy.StickyUntil > DateTime.UtcNow,
        PrintStickyMode.UntilFailure => true,
        PrintStickyMode.Permanent => true,
        _ => false
    };

}

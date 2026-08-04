using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Printing;

public sealed class PrintRoutingService(IApplicationDbContext db)
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
        var onlineAfter = DateTime.UtcNow.AddSeconds(-45);
        var endpoints = await db.PrinterEndpoints
            .Include(x => x.PrintNode)
            .Where(x => x.PrintNode.BranchId == job.BranchId
                && x.IsEnabled
                && x.PrintNode.IsEnabled
                && x.PrintNode.HostEnabled
                && x.PrintNode.LastSeenAt >= onlineAfter
                && (x.Capabilities & capability) == capability
                && (x.Status == PrinterEndpointStatus.Ready || x.Status == PrinterEndpointStatus.Busy))
            .ToListAsync(cancellationToken);

        var attempted = await db.PrintAttempts
            .Where(x => x.PrintJobId == job.Id)
            .Select(x => new { x.PrinterEndpointId, FailedAt = x.CompletedAt ?? x.StartedAt })
            .ToListAsync(cancellationToken);
        endpoints.RemoveAll(endpoint => attempted.Any(attempt => attempt.PrinterEndpointId == endpoint.Id
            && (endpoint.LastSeenAt ?? DateTime.MinValue) <= attempt.FailedAt));

        var endpoint = SelectEndpoint(job, policy, endpoints);
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
        _ => PrintCapability.None
    };

    private static PrinterEndpoint? SelectEndpoint(
        PrintJob job,
        PrintRoutingPolicy policy,
        IReadOnlyCollection<PrinterEndpoint> endpoints)
    {
        if (endpoints.Count == 0) return null;

        if (policy.RoutingMode is PrintRoutingMode.LocalFirst or PrintRoutingMode.LocalOnly && job.OriginNodeId is not null)
        {
            var local = endpoints.FirstOrDefault(x => x.PrintNodeId == job.OriginNodeId);
            if (local is not null) return local;
            if (policy.RoutingMode == PrintRoutingMode.LocalOnly) return null;
        }

        if (IsStickyValid(policy))
        {
            var sticky = endpoints.FirstOrDefault(x => x.Id == policy.StickyEndpointId);
            if (sticky is not null) return sticky;
        }

        var priorities = policy.Targets
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Priority)
            .Select(x => x.PrinterEndpointId)
            .ToList();
        foreach (var endpointId in priorities)
        {
            var configured = endpoints.FirstOrDefault(x => x.Id == endpointId);
            if (configured is not null) return configured;
        }

        return policy.AllowFallback
            ? endpoints.OrderByDescending(x => x.LastSuccessAt).ThenBy(x => x.Id).FirstOrDefault()
            : null;
    }

    private static bool IsStickyValid(PrintRoutingPolicy policy) => policy.StickyEndpointId is not null && policy.StickyMode switch
    {
        PrintStickyMode.Disabled => false,
        PrintStickyMode.Duration => policy.StickyUntil > DateTime.UtcNow,
        PrintStickyMode.UntilFailure => true,
        PrintStickyMode.Permanent => true,
        _ => false
    };

}

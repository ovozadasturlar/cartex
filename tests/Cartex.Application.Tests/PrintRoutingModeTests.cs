using Cartex.Application.Common.Messaging;
using Cartex.Application.Printing;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainCapability = Cartex.Domain.Enums.PrintCapability;
using DomainEndpointStatus = Cartex.Domain.Enums.PrinterEndpointStatus;
using DomainJobKind = Cartex.Domain.Enums.PrintJobKind;
using DomainJobStatus = Cartex.Domain.Enums.PrintJobStatus;
using DomainNodeStatus = Cartex.Domain.Enums.PrintNodeStatus;
using DomainRoutingMode = Cartex.Domain.Enums.PrintRoutingMode;
using DomainStickyMode = Cartex.Domain.Enums.PrintStickyMode;
using SharedRoutingMode = Cartex.Shared.Models.Printing.PrintRoutingMode;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class PrintRoutingModeTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task CHOP_09_LocalFirst_prefers_the_requesting_devices_printer()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, adminId) = await CreateContextAsync(scope);
        var local = CreateNode(branchId, "desktop", "Local printer");
        var priority = CreateNode(branchId, "remote", "Priority printer");
        db.PrintNodes.AddRange(local, priority);
        await db.SaveChangesAsync();
        var job = CreateJob(branchId, adminId, local.Id);
        db.PrintJobs.Add(job);
        await ConfigurePolicyAsync(db, routing, branchId, false, priority.Endpoints.Single());

        Assert.True(await routing.AssignAsync(job, default));
        Assert.Equal(local.Endpoints.Single().Id, job.AssignedEndpointId);
        Assert.Equal(DomainJobStatus.Assigned, job.Status);
    }

    [Fact]
    public async Task CHOP_09_LocalFirst_uses_priority_when_the_requesting_device_has_no_printer()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, adminId) = await CreateContextAsync(scope);
        var priority = CreateNode(branchId, "remote", "Priority printer");
        db.PrintNodes.Add(priority);
        await db.SaveChangesAsync();
        var job = CreateJob(branchId, adminId);
        db.PrintJobs.Add(job);
        await ConfigurePolicyAsync(db, routing, branchId, false, priority.Endpoints.Single());

        Assert.True(await routing.AssignAsync(job, default));
        Assert.Equal(priority.Endpoints.Single().Id, job.AssignedEndpointId);
        Assert.Equal(DomainJobStatus.Assigned, job.Status);
    }

    [Fact]
    public async Task CHOP_09_LocalFirst_without_local_priority_or_fallback_leaves_the_job_pending()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, adminId) = await CreateContextAsync(scope);
        db.PrintNodes.Add(CreateNode(branchId, "remote", "Unlisted printer"));
        var job = CreateJob(branchId, adminId);
        db.PrintJobs.Add(job);
        await ConfigurePolicyAsync(db, routing, branchId, false);

        Assert.False(await routing.AssignAsync(job, default));
        Assert.Null(job.AssignedEndpointId);
        Assert.Equal(DomainJobStatus.Pending, job.Status);
    }

    [Fact]
    public async Task CHOP_09_Unknown_saved_mode_is_normalized_by_single_and_list_reads()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, _) = await CreateContextAsync(scope);
        db.PrintRoutingPolicies.Add(new PrintRoutingPolicy
        {
            BranchId = branchId,
            Kind = DomainJobKind.Receipt,
            IsEnabled = true,
            RoutingMode = (DomainRoutingMode)2
        });
        await db.SaveChangesAsync();

        var policy = await routing.GetOrCreatePolicyAsync(branchId, DomainJobKind.Receipt, default);
        Assert.Equal(DomainRoutingMode.LocalFirst, policy.RoutingMode);
        Assert.Equal(DomainRoutingMode.LocalFirst,
            await db.PrintRoutingPolicies.AsNoTracking().Select(x => x.RoutingMode).SingleAsync());

        policy.RoutingMode = (DomainRoutingMode)2;
        await db.SaveChangesAsync();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var listed = await sender.Send(new GetPrintRoutingPoliciesQuery(branchId));

        Assert.Equal(SharedRoutingMode.LocalFirst,
            listed.Single(x => x.Kind == Cartex.Shared.Models.Printing.PrintJobKind.Receipt).RoutingMode);
        db.ChangeTracker.Clear();
        Assert.Equal(DomainRoutingMode.LocalFirst,
            await db.PrintRoutingPolicies.AsNoTracking()
                .Where(x => x.Kind == DomainJobKind.Receipt)
                .Select(x => x.RoutingMode)
                .SingleAsync());
    }

    private async Task<(ApplicationDbContext Db, PrintRoutingService Routing, long BranchId, long AdminId)> CreateContextAsync(
        IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).SingleAsync();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        return (db, scope.ServiceProvider.GetRequiredService<PrintRoutingService>(), branchId, adminId);
    }

    private static PrintNode CreateNode(long branchId, string deviceId, string printerName) => new()
    {
        BranchId = branchId,
        DeviceId = deviceId,
        CredentialHash = PrintingCredential.Hash(PrintingCredential.Issue()),
        Name = deviceId,
        IsTrusted = true,
        HostEnabled = true,
        Status = DomainNodeStatus.Online,
        LastSeenAt = DateTime.UtcNow,
        Endpoints =
        {
            new PrinterEndpoint
            {
                StableKey = $"{deviceId}-printer",
                DisplayName = printerName,
                SystemName = printerName,
                Capabilities = DomainCapability.Receipt,
                Status = DomainEndpointStatus.Ready,
                IsEnabled = true,
                LastSeenAt = DateTime.UtcNow
            }
        }
    };

    private static PrintJob CreateJob(long branchId, long adminId, long? originNodeId = null) => new()
    {
        BranchId = branchId,
        Kind = DomainJobKind.Receipt,
        SourceType = "receipt_token",
        SourceId = Guid.NewGuid().ToString("N"),
        PayloadJson = "{}",
        RequestedByUserId = adminId,
        OriginNodeId = originNodeId
    };

    private static async Task ConfigurePolicyAsync(
        ApplicationDbContext db,
        PrintRoutingService routing,
        long branchId,
        bool allowFallback,
        PrinterEndpoint? priority = null)
    {
        var policy = await routing.GetOrCreatePolicyAsync(branchId, DomainJobKind.Receipt, default);
        policy.IsEnabled = true;
        policy.RoutingMode = DomainRoutingMode.LocalFirst;
        policy.AllowFallback = allowFallback;
        policy.StickyMode = DomainStickyMode.Disabled;
        policy.Targets.Clear();
        if (priority is not null)
        {
            policy.Targets.Add(new PrintRouteTarget
            {
                PrinterEndpointId = priority.Id,
                Priority = 1,
                IsEnabled = true
            });
        }

        await db.SaveChangesAsync();
    }
}

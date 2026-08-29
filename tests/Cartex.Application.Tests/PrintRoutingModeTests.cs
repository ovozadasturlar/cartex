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
    public async Task CHOP_09_Unknown_saved_mode_is_read_as_local_first()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, _) = await CreateContextAsync(scope);
        await routing.GetOrCreatePolicyAsync(branchId, DomainJobKind.Receipt, default);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE print_routing_policies SET routing_mode = 'RetiredMode' WHERE branch_id = {0} AND kind = 'Receipt'",
            branchId);
        db.ChangeTracker.Clear();

        var policy = await routing.GetOrCreatePolicyAsync(branchId, DomainJobKind.Receipt, default);
        Assert.Equal(DomainRoutingMode.LocalFirst, policy.RoutingMode);

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var listed = await sender.Send(new GetPrintRoutingPoliciesQuery(branchId));
        Assert.Equal(SharedRoutingMode.LocalFirst,
            listed.Single(x => x.Kind == Cartex.Shared.Models.Printing.PrintJobKind.Receipt).RoutingMode);
    }

    [Fact]
    public async Task CHOP_10_LocalOnly_sends_another_devices_job_to_the_selected_printer()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, adminId) = await CreateContextAsync(scope);
        var selected = CreateNode(branchId, "kassa", "Kassa printeri");
        var origin = CreateNode(branchId, "desktop", "Boshqa printer");
        db.PrintNodes.AddRange(selected, origin);
        await db.SaveChangesAsync();
        var job = CreateJob(branchId, adminId, origin.Id);
        db.PrintJobs.Add(job);
        await ConfigurePolicyAsync(db, routing, branchId, true, DomainRoutingMode.LocalOnly, selected.Endpoints.Single());

        Assert.True(await routing.AssignAsync(job, default));
        Assert.Equal(selected.Endpoints.Single().Id, job.AssignedEndpointId);
    }

    [Fact]
    public async Task CHOP_10_LocalOnly_without_a_selected_printer_assigns_nobody()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, adminId) = await CreateContextAsync(scope);
        var origin = CreateNode(branchId, "desktop", "Boshqa printer");
        db.PrintNodes.Add(origin);
        await db.SaveChangesAsync();
        var job = CreateJob(branchId, adminId, origin.Id);
        db.PrintJobs.Add(job);
        await ConfigurePolicyAsync(db, routing, branchId, true, DomainRoutingMode.LocalOnly);

        Assert.False(await routing.AssignAsync(job, default));
        Assert.Null(job.AssignedEndpointId);
        Assert.Equal(DomainJobStatus.Pending, job.Status);
    }

    [Fact]
    public async Task CHOP_11_A_host_that_is_not_connected_is_not_assigned()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, adminId) = await CreateContextAsync(scope);
        var host = CreateNode(branchId, "kassa", "Kassa printeri");
        db.PrintNodes.Add(host);
        await db.SaveChangesAsync();
        var job = CreateJob(branchId, adminId);
        db.PrintJobs.Add(job);
        await ConfigurePolicyAsync(db, routing, branchId, true, DomainRoutingMode.LocalFirst, host.Endpoints.Single());
        Fixture.HubPresence.PrintHostOffline("kassa");

        Assert.False(await routing.AssignAsync(job, default));
        Assert.Equal(DomainJobStatus.Pending, job.Status);
    }

    [Fact]
    public async Task CHOP_11_Job_moves_on_when_the_assigned_host_disconnects()
    {
        using var scope = Fixture.CreateScope();
        var (db, routing, branchId, adminId) = await CreateContextAsync(scope);
        var host = CreateNode(branchId, "kassa", "Kassa printeri");
        var backup = CreateNode(branchId, "zaxira", "Zaxira printeri");
        db.PrintNodes.AddRange(host, backup);
        await db.SaveChangesAsync();
        var job = CreateJob(branchId, adminId);
        db.PrintJobs.Add(job);
        await ConfigurePolicyAsync(db, routing, branchId, false, DomainRoutingMode.PriorityOnly,
            host.Endpoints.Single(), backup.Endpoints.Single());

        Assert.True(await routing.AssignAsync(job, default));
        Assert.Equal(host.Endpoints.Single().Id, job.AssignedEndpointId);

        Fixture.HubPresence.PrintHostOffline("kassa");
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecoverPrintJobsCommand());

        db.ChangeTracker.Clear();
        var reloaded = await db.PrintJobs.Include(x => x.Attempts).SingleAsync(x => x.Id == job.Id);
        Assert.Equal(backup.Endpoints.Single().Id, reloaded.AssignedEndpointId);
        Assert.Contains(reloaded.Attempts, x => x.ErrorCode == "host_offline");
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

    private static Task ConfigurePolicyAsync(
        ApplicationDbContext db,
        PrintRoutingService routing,
        long branchId,
        bool allowFallback,
        PrinterEndpoint? priority = null) =>
        ConfigurePolicyAsync(db, routing, branchId, allowFallback, DomainRoutingMode.LocalFirst,
            priority is null ? [] : [priority]);

    private static async Task ConfigurePolicyAsync(
        ApplicationDbContext db,
        PrintRoutingService routing,
        long branchId,
        bool allowFallback,
        DomainRoutingMode mode,
        params PrinterEndpoint[] priorities)
    {
        var policy = await routing.GetOrCreatePolicyAsync(branchId, DomainJobKind.Receipt, default);
        policy.IsEnabled = true;
        policy.RoutingMode = mode;
        policy.AllowFallback = allowFallback;
        policy.StickyMode = DomainStickyMode.Disabled;
        policy.Targets.Clear();
        for (var i = 0; i < priorities.Length; i++)
        {
            policy.Targets.Add(new PrintRouteTarget
            {
                PrinterEndpointId = priorities[i].Id,
                Priority = i + 1,
                IsEnabled = true
            });
        }

        await db.SaveChangesAsync();
    }
}

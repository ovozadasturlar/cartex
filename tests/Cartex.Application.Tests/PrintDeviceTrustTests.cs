using Cartex.Application.Common.Messaging;
using Cartex.Application.Printing;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainEndpointStatus = Cartex.Domain.Enums.PrinterEndpointStatus;
using DomainJobKind = Cartex.Domain.Enums.PrintJobKind;
using DomainJobStatus = Cartex.Domain.Enums.PrintJobStatus;
using DomainCapability = Cartex.Domain.Enums.PrintCapability;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class PrintDeviceTrustTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private static RegisterPrintNodeRequest Registration(string deviceId, long branchId, string? token = null) =>
        new(deviceId, $"Till {deviceId}", branchId, "1.0", true,
            [new PrinterEndpointRegistration("key-1", "XP-58", "XP-58", PrintCapability.Receipt, PrinterEndpointStatus.Ready)],
            token);

    private async Task<(long BranchId, long AdminId, long BusinessId)> SeedContextAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).SingleAsync();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        return (branchId, adminId, businessId);
    }

    [Fact]
    public async Task New_node_follows_branch_auto_trust()
    {
        var (branchId, _, _) = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var first = await sender.Send(new RegisterPrintNodeCommand(Registration("dev-plain", branchId)));
        Assert.False(string.IsNullOrWhiteSpace(first.HostToken));
        Assert.False(await db.PrintNodes.Where(x => x.DeviceId == "dev-plain").Select(x => x.IsTrusted).SingleAsync());

        await sender.Send(new SetPrintAutoTrustCommand(new SetPrintAutoTrustRequest(branchId, true)));
        await sender.Send(new RegisterPrintNodeCommand(Registration("dev-auto", branchId)));
        Assert.True(await db.PrintNodes.Where(x => x.DeviceId == "dev-auto").Select(x => x.IsTrusted).SingleAsync());
    }

    [Fact]
    public async Task Lost_credential_is_replaced_silently_and_trust_survives()
    {
        var (branchId, _, _) = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var issued = await sender.Send(new RegisterPrintNodeCommand(Registration("dev-lost", branchId)));
        await sender.Send(new SetPrintDeviceTrustCommand(new SetPrintDeviceTrustRequest(branchId, "dev-lost", true)));
        Fixture.HubPresence.PrintHostOffline("dev-lost");

        var again = await sender.Send(new RegisterPrintNodeCommand(Registration("dev-lost", branchId, "wrong-token")));
        Assert.False(string.IsNullOrWhiteSpace(again.HostToken));
        Assert.NotEqual(issued.HostToken, again.HostToken);
        var node = await db.PrintNodes.AsNoTracking().SingleAsync(x => x.DeviceId == "dev-lost");
        Assert.True(node.IsTrusted);
        Assert.True(PrintingCredential.Matches(node.CredentialHash, again.HostToken));
    }

    [Fact]
    public async Task Active_holder_cannot_be_displaced()
    {
        var (branchId, _, _) = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new RegisterPrintNodeCommand(Registration("dev-live", branchId)));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new RegisterPrintNodeCommand(Registration("dev-live", branchId, "attacker-token"))));
    }

    [Fact]
    public async Task Machine_identity_change_renames_the_node_and_keeps_trust()
    {
        var (branchId, _, _) = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var issued = await sender.Send(new RegisterPrintNodeCommand(Registration("dev-old", branchId)));
        db.PrintRequesterDevices.Add(new PrintRequesterDevice
        {
            BranchId = branchId,
            DeviceId = "dev-old",
            Name = "Till dev-old",
            IsTrusted = true
        });
        await db.SaveChangesAsync();
        await sender.Send(new SetPrintDeviceTrustCommand(new SetPrintDeviceTrustRequest(branchId, "dev-old", true)));

        await sender.Send(new RegisterPrintNodeCommand(Registration("dev-new", branchId, issued.HostToken)));
        Assert.False(await db.PrintNodes.AnyAsync(x => x.DeviceId == "dev-old"));
        var node = await db.PrintNodes.AsNoTracking().SingleAsync(x => x.DeviceId == "dev-new");
        Assert.True(node.IsTrusted);
        Assert.True(await db.PrintRequesterDevices.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.DeviceId == "dev-new")
            .Select(x => x.IsTrusted).SingleAsync());
    }

    [Fact]
    public async Task Requester_record_inherits_trust_from_the_host_node()
    {
        var (branchId, _, _) = await SeedContextAsync();
        await TestShift.OpenAsync(Fixture);
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await sender.Send(new RegisterPrintNodeCommand(Registration("dev-inherit", branchId)));
        await sender.Send(new SetPrintDeviceTrustCommand(new SetPrintDeviceTrustRequest(branchId, "dev-inherit", true)));
        var shiftId = await db.Shifts.Select(x => x.Id).FirstAsync();

        var job = await sender.Send(new CreatePrintJobCommand(new CreatePrintJobRequest(
            branchId, PrintJobKind.ZReport, "shift", shiftId.ToString(),
            System.Text.Json.JsonSerializer.SerializeToElement(new { shiftId }),
            DeviceId: "dev-inherit", DeviceName: "Till")));

        Assert.NotEqual(PrintJobStatus.Rejected, job.Status);
        Assert.True(await db.PrintRequesterDevices.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.DeviceId == "dev-inherit")
            .Select(x => x.IsTrusted).SingleAsync());
    }

    [Fact]
    public async Task New_node_inherits_trust_from_the_requester_record()
    {
        var (branchId, _, _) = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.PrintRequesterDevices.Add(new PrintRequesterDevice
        {
            BranchId = branchId,
            DeviceId = "dev-phone-first",
            Name = "Till",
            IsTrusted = true
        });
        await db.SaveChangesAsync();

        await sender.Send(new RegisterPrintNodeCommand(Registration("dev-phone-first", branchId)));
        Assert.True(await db.PrintNodes.AsNoTracking()
            .Where(x => x.DeviceId == "dev-phone-first").Select(x => x.IsTrusted).SingleAsync());
    }

    [Fact]
    public async Task Deleting_a_device_keeps_history_and_lets_it_come_back()
    {
        var (branchId, _, _) = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await sender.Send(new RegisterPrintNodeCommand(Registration("dev-gone", branchId)));
        var node = await db.PrintNodes.Include(x => x.Endpoints).SingleAsync(x => x.DeviceId == "dev-gone");
        db.PrintRequesterDevices.Add(new PrintRequesterDevice
        {
            BranchId = branchId,
            DeviceId = "dev-gone",
            Name = "Till",
            IsTrusted = true
        });
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        var job = new PrintJob
        {
            BranchId = branchId,
            Kind = DomainJobKind.Receipt,
            Status = DomainJobStatus.Completed,
            SourceType = "receipt_token",
            SourceId = "t",
            PayloadJson = "{}",
            RequestedByUserId = adminId,
            Attempts =
            {
                new PrintAttempt
                {
                    AttemptNumber = 1,
                    PrintNodeId = node.Id,
                    PrinterEndpointId = node.Endpoints.First().Id,
                    LeaseToken = "lease-1"
                }
            }
        };
        db.PrintJobs.Add(job);
        await db.SaveChangesAsync();

        await sender.Send(new DeletePrintDeviceCommand(branchId, "dev-gone"));

        Assert.False(await db.PrintNodes.AnyAsync(x => x.DeviceId == "dev-gone"));
        Assert.False(await db.PrintRequesterDevices.AnyAsync(x => x.DeviceId == "dev-gone"));
        var attempt = await db.PrintAttempts.AsNoTracking().SingleAsync(x => x.PrintJobId == job.Id);
        Assert.Null(attempt.PrintNodeId);
        Assert.Null(attempt.PrinterEndpointId);

        var again = await sender.Send(new RegisterPrintNodeCommand(Registration("dev-gone", branchId)));
        Assert.False(string.IsNullOrWhiteSpace(again.HostToken));
        Assert.False(await db.PrintNodes.Where(x => x.DeviceId == "dev-gone")
            .Select(x => x.IsTrusted).SingleAsync());
    }

    [Fact]
    public async Task Assignment_falls_back_to_a_sleeping_printer_when_no_live_one_exists()
    {
        var (branchId, _, _) = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var routing = scope.ServiceProvider.GetRequiredService<PrintRoutingService>();

        var node = new PrintNode
        {
            BranchId = branchId,
            DeviceId = "dev-sleepy",
            CredentialHash = PrintingCredential.Hash(PrintingCredential.Issue()),
            Name = "Sleepy till",
            IsTrusted = true,
            HostEnabled = true,
                LastSeenAt = DateTime.UtcNow,
            Endpoints =
            {
                new PrinterEndpoint
                {
                    StableKey = "k",
                    DisplayName = "HP LaserJet",
                    SystemName = "HP LaserJet",
                    Capabilities = DomainCapability.Receipt,
                    Status = DomainEndpointStatus.Offline
                }
            }
        };
        db.PrintNodes.Add(node);
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        var job = new PrintJob
        {
            BranchId = branchId,
            Kind = DomainJobKind.Receipt,
            SourceType = "receipt_token",
            SourceId = "t",
            PayloadJson = "{}",
            RequestedByUserId = adminId
        };
        db.PrintJobs.Add(job);
        await db.SaveChangesAsync();

        Assert.True(await routing.AssignAsync(job, default));
        Assert.Equal(node.Endpoints.First().Id, job.AssignedEndpointId);
    }

    [Fact]
    public async Task Offline_completed_job_is_recorded_without_routing()
    {
        var (branchId, _, _) = await SeedContextAsync();
        await TestShift.OpenAsync(Fixture);
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await sender.Send(new SetPrintAutoTrustCommand(new SetPrintAutoTrustRequest(branchId, true)));
        var shiftId = await db.Shifts.Select(x => x.Id).FirstAsync();

        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new { shiftId });
        var request = new CreatePrintJobRequest(
            branchId, PrintJobKind.ZReport, "shift", shiftId.ToString(), payload,
            IdempotencyKey: "offline:test:1", DeviceId: "dev-offline", DeviceName: "Till", CompletedLocally: true);
        var job = await sender.Send(new CreatePrintJobCommand(request));

        Assert.Equal(PrintJobStatus.Completed, job.Status);
        Assert.Null(job.AssignedNodeId);
        var stored = await db.PrintJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
        Assert.Equal(DomainJobStatus.Completed, stored.Status);
        Assert.NotNull(stored.CompletedAt);
        Assert.False(await db.PrintAttempts.AnyAsync(x => x.PrintJobId == job.Id));

        var replay = await sender.Send(new CreatePrintJobCommand(request));
        Assert.Equal(job.Id, replay.Id);
    }
}

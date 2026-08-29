using System.Text.Json;
using Cartex.Application.Common.Messaging;
using Cartex.Application.OfflineCache.Commands;
using Cartex.Domain.Authorization;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Application.Tests.Common;

public static class TestOffline
{
    public const string Device = "test-offline-device";

    public static async Task<OfflineLeaseGrantDto> ClaimAsync(DatabaseFixture fixture, long warehouseId, string device = Device)
    {
        using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var feature = await db.Features.FirstOrDefaultAsync(f => f.Code == FeatureCatalog.OfflineCache);
            if (feature is null)
                db.Features.Add(new Feature { Code = FeatureCatalog.OfflineCache, Name = "Offline", IsEnabled = true, OwnerEnabled = true });
            else
            {
                feature.IsEnabled = true;
                feature.OwnerEnabled = true;
            }
            await db.SaveChangesAsync();
        }

        fixture.CurrentUser.DeviceId = device;
        using var claimScope = fixture.CreateScope();
        var sender = claimScope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new ClaimOfflineCacheCommand(device, "Test kassa", warehouseId));
    }

    public static OfflineSyncEventRequest Event(long sequence, string kind, object payload, long? actorUserId = null) =>
        new(Guid.NewGuid(), sequence, kind, $"evt-{Guid.NewGuid():N}", DateTime.UtcNow,
            JsonSerializer.SerializeToElement(payload, JsonSerializerOptions.Web), actorUserId);

    public static async Task<OfflineSyncBatchResult> PushAsync(DatabaseFixture fixture, OfflineLeaseGrantDto grant,
        params OfflineSyncEventRequest[] events)
    {
        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new ProcessOfflineSyncBatch(grant.LeaseId, grant.Epoch, grant.LeaseToken, events));
    }

    public static async Task<OfflineSyncBatchResult> ImportAsync(DatabaseFixture fixture, long leaseId, long epoch,
        string leaseToken, bool skipRejected, params OfflineSyncEventRequest[] events)
    {
        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new ImportOfflineSyncCommand(leaseId, epoch, leaseToken, events, skipRejected));
    }

    public static Task<OfflineSyncBatchResult> ImportAsync(DatabaseFixture fixture, OfflineLeaseGrantDto grant,
        bool skipRejected = false, params OfflineSyncEventRequest[] events) =>
        ImportAsync(fixture, grant.LeaseId, grant.Epoch, grant.LeaseToken, skipRejected, events);

    public static async Task ForceReleaseAsync(DatabaseFixture fixture, long leaseId, string? reason = null)
    {
        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new ReleaseOfflineCacheCommand(leaseId, null, true, reason));
    }

    public static async Task<OfflineSyncEventResult> SkipAsync(DatabaseFixture fixture, OfflineLeaseGrantDto grant,
        OfflineSyncEventRequest e, string? reason = null)
    {
        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new SkipOfflineSyncEventCommand(grant.LeaseId, grant.Epoch, grant.LeaseToken, e, reason));
    }

    public static async Task<long> LastAcceptedSequenceAsync(DatabaseFixture fixture, long leaseId)
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.OfflineAuthorityLeases.AsNoTracking().SingleAsync(x => x.Id == leaseId)).LastAcceptedSequence;
    }

    public static async Task<long> CreateActorAsync(DatabaseFixture fixture, long branchId, params string[] permissions)
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User { FullName = $"Actor {suffix}", Username = $"actor-{suffix}", PasswordHash = "x", DefaultBranchId = branchId };
        db.Users.Add(user);
        db.UserBranches.Add(new UserBranch { User = user, BranchId = branchId });
        if (permissions.Length > 0)
        {
            var role = new Role { Name = $"actor-role-{suffix}", IsActive = true };
            db.Roles.Add(role);
            foreach (var name in permissions)
            {
                var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Name == name)
                    ?? db.Permissions.Add(new Permission { Name = name }).Entity;
                db.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
            }
            db.UserRoles.Add(new UserRole { User = user, Role = role });
        }
        await db.SaveChangesAsync();
        return user.Id;
    }
}

using System.Security.Cryptography;
using System.Text;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.OfflineCache;

internal static class OfflineLeaseSecurity
{
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static bool TokenMatches(string token, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(token) || expectedHash.Length != 64)
            return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(token)),
                Convert.FromHexString(expectedHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static async Task<OfflineAuthorityLease> RequireActiveAsync(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        long leaseId,
        long epoch,
        string token,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? throw new UnauthorizedAccessException("Business context is missing.");
        var query = forUpdate
            ? db.OfflineAuthorityLeases.FromSqlInterpolated(
                $"SELECT * FROM offline_authority_leases WHERE id = {leaseId} FOR UPDATE")
            : db.OfflineAuthorityLeases.AsQueryable();
        var lease = await query.FirstOrDefaultAsync(x =>
                x.Id == leaseId && x.BusinessId == businessId,
                cancellationToken)
            ?? throw new NotFoundException("Oflayn vakolat topilmadi.", "offline_lease_not_found");

        if (lease.RevokedAt is not null)
            throw new ConflictException("Oflayn vakolat bekor qilingan.", "offline_lease_revoked");
        if (lease.Epoch != epoch)
            throw new ConflictException("Oflayn vakolat versiyasi eskirgan.", "offline_lease_epoch_mismatch");
        if (!string.Equals(currentUser.DeviceId, lease.DeviceId, StringComparison.Ordinal))
            throw new ForbiddenException("Bu vakolat boshqa qurilmaga tegishli.", "offline_lease_device_mismatch");
        if (!TokenMatches(token, lease.TokenHash))
            throw new ForbiddenException("Oflayn vakolat kaliti noto'g'ri.", "offline_lease_token_invalid");

        return lease;
    }
}

public interface IOfflineAuthorityGuard
{
    Task EnsureOnlineMutationAllowedAsync(long branchId, CancellationToken cancellationToken);
}

public sealed class OfflineAuthorityGuard(IApplicationDbContext db, ICurrentUser currentUser)
    : IOfflineAuthorityGuard
{
    // A fresh holder is connected to the same server and normal multi-device work is safe.
    // A stale heartbeat means that the holder may be selling from its local snapshot.
    private static readonly TimeSpan SplitBrainThreshold = TimeSpan.FromSeconds(60);

    public async Task EnsureOnlineMutationAllowedAsync(long branchId, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? await db.Branches.Where(x => x.Id == branchId)
                .Select(x => (long?)x.BusinessId)
                .FirstOrDefaultAsync(cancellationToken);
        if (businessId is null) return;

        var active = await db.OfflineAuthorityLeases.AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.RevokedAt == null)
            .Select(x => new { x.DeviceId, x.DeviceName, x.LastHeartbeatAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (active is null || string.Equals(active.DeviceId, currentUser.DeviceId, StringComparison.Ordinal))
            return;

        if (active.LastHeartbeatAt < DateTime.UtcNow - SplitBrainThreshold)
            throw new ConflictException(
                $"\"{active.DeviceName}\" qurilmasi oflayn savdo vakolatiga ega. Inventar to'qnashuvini oldini olish uchun savdo vaqtincha bloklandi.",
                "offline_authority_possibly_active");
    }
}

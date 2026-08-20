using System.Security.Cryptography;
using System.Text;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
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

    // OFF-41: import uchun qurilma mosligi talab qilinmaydi (qurilma o'lgan) va bekor
    // qilingan lease ham qabul qilinadi — amallar tarixiy faktlar. Token isbot bo'lib qoladi.
    public static async Task<OfflineAuthorityLease> RequireForImportAsync(
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

        if (lease.Epoch != epoch)
            throw new ConflictException("Oflayn vakolat versiyasi eskirgan.", "offline_lease_epoch_mismatch");
        if (!TokenMatches(token, lease.TokenHash))
            throw new ForbiddenException("Oflayn vakolat kaliti noto'g'ri.", "offline_lease_token_invalid");

        return lease;
    }
}

public interface IOfflineAuthorityGuard
{
    Task EnsureOnlineMutationAllowedAsync(long branchId, long warehouseId, CancellationToken cancellationToken);

    /// OFF-16: vakolat egasi jim bo'lgan oyna. Qoldiq nazorati faqat shu oynada yumshatiladi.
    Task<bool> IsOfflineWindowOpenAsync(long warehouseId, CancellationToken cancellationToken);
}

public sealed class OfflineAuthorityGuard(IApplicationDbContext db, ICurrentUser currentUser, ISettingsService settings)
    : IOfflineAuthorityGuard
{
    // A fresh holder is connected to the same server and normal multi-device work is safe.
    // A stale heartbeat means that the holder may be selling from its local snapshot.
    private static readonly TimeSpan SplitBrainThreshold = TimeSpan.FromSeconds(60);

    // OFF-19: bir kundan beri javob bermagan vakolat "tashlab ketilgan" hisoblanadi — aks holda
    // yo'qolgan qurilma do'konni abadiy blokda yoki abadiy minus qoldiq rejimida ushlab turardi.
    private static readonly TimeSpan AbandonedThreshold = TimeSpan.FromHours(24);

    public async Task EnsureOnlineMutationAllowedAsync(long branchId, long warehouseId, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? await db.Branches.Where(x => x.Id == branchId)
                .Select(x => (long?)x.BusinessId)
                .FirstOrDefaultAsync(cancellationToken);
        if (businessId is null) return;

        var holder = await SilentHolderAsync(businessId.Value, warehouseId, cancellationToken);
        if (holder is null) return;

        // OFF-15/OFF-14: minus qoldiqni tan olgan do'kon uchun blok ma'nosiz — to'qnashuv baribir
        // minus qoldiq sifatida o'z-o'zidan hal bo'ladi. OFF-16 xuddi shu yumshatishni beradi,
        // faqat shu oyna ichida — shuning uchun ikkala kalit ham blokni ochadi.
        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
            ?? new SalesPolicySettings();
        if (policy.AllowInsufficientStockSales || policy.AllowNegativeStockWhenOffline)
            return;

        throw new ConflictException(
            $"\"{holder}\" qurilmasi oflayn savdo vakolatiga ega. Inventar to'qnashuvini oldini olish uchun savdo vaqtincha bloklandi.",
            "offline_authority_possibly_active");
    }

    public async Task<bool> IsOfflineWindowOpenAsync(long warehouseId, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? await db.Warehouses.Where(x => x.Id == warehouseId)
                .Select(x => (long?)x.Branch.BusinessId)
                .FirstOrDefaultAsync(cancellationToken);
        return businessId is not null
            && await SilentHolderAsync(businessId.Value, warehouseId, cancellationToken) is not null;
    }

    /// Oyna yagona joyda o'lchanadi: qo'riqchi savdoni o'tkazib yuborib, qoldiq nazorati o'sha
    /// savdoni rad etadigan yoriq qolmasligi uchun.
    private async Task<string?> SilentHolderAsync(long businessId, long warehouseId, CancellationToken cancellationToken)
    {
        // OFF-15: jismoniy to'qnashuv faqat vakolat omborida mumkin — boshqa ombor bloklanmaydi.
        var active = await db.OfflineAuthorityLeases.AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.WarehouseId == warehouseId && x.RevokedAt == null)
            .Select(x => new { x.DeviceId, x.DeviceName, x.LastHeartbeatAt })
            .FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        return active is not null
            && !string.Equals(active.DeviceId, currentUser.DeviceId, StringComparison.Ordinal)
            && active.LastHeartbeatAt < now - SplitBrainThreshold
            && active.LastHeartbeatAt > now - AbandonedThreshold
            ? active.DeviceName
            : null;
    }
}

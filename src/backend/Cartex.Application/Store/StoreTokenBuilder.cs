using Cartex.Application.Auth;
using Cartex.Auth.Services;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store;

public record StoreLoginResponse(string Token, string RefreshToken, string FullName);

public sealed class StoreTokenBuilder(IApplicationDbContext db, IJwtTokenGenerator jwtTokenGenerator)
{
    private const int RefreshLifetimeDays = 30;
    private const int AbsoluteLifetimeDays = 90;
    private const int ReuseGraceSeconds = 30;

    public async Task<StoreLoginResponse> IssueAsync(Customer customer, string? deviceName, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var refreshToken = CreateSession(customer.Id, deviceName, now, out _);
        await db.SaveChangesAsync(cancellationToken);
        return new StoreLoginResponse(jwtTokenGenerator.GenerateCustomerToken(customer.Id, customer.FullName), refreshToken, customer.FullName);
    }

    public async Task<StoreLoginResponse?> RotateAsync(string rawRefresh, string? deviceName, CancellationToken cancellationToken)
    {
        var hash = RefreshTokens.Hash(rawRefresh);
        var session = await db.CustomerSessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
        if (session is null) return null;

        var now = DateTime.UtcNow;

        if (session.RevokedAt is not null)
        {
            var supersededByRotation = session.ReplacedByHash is not null;
            var pastGrace = session.RevokedAt <= now.AddSeconds(-ReuseGraceSeconds);
            if (supersededByRotation && pastGrace)
                await db.CustomerSessions
                    .Where(s => s.CustomerId == session.CustomerId && s.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
            return null;
        }

        if (session.ExpiresAt <= now) return null;

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == session.CustomerId, cancellationToken);
        if (customer is null) return null;

        var newRaw = RefreshTokens.Generate();
        var newHash = RefreshTokens.Hash(newRaw);

        var claimed = await db.CustomerSessions
            .Where(s => s.Id == session.Id && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.ReplacedByHash, newHash), cancellationToken);
        if (claimed == 0) return null;

        AddSession(customer.Id, deviceName ?? session.DeviceName, newRaw, newHash, now, session.FamilyCreatedAt);
        await db.SaveChangesAsync(cancellationToken);
        return new StoreLoginResponse(jwtTokenGenerator.GenerateCustomerToken(customer.Id, customer.FullName), newRaw, customer.FullName);
    }

    private string CreateSession(long customerId, string? deviceName, DateTime now, out string hash)
    {
        var raw = RefreshTokens.Generate();
        hash = RefreshTokens.Hash(raw);
        AddSession(customerId, deviceName, raw, hash, now, now);
        return raw;
    }

    private void AddSession(long customerId, string? deviceName, string raw, string hash, DateTime now, DateTime familyCreatedAt)
    {
        var absolute = familyCreatedAt.AddDays(AbsoluteLifetimeDays);
        var expires = now.AddDays(RefreshLifetimeDays);
        db.CustomerSessions.Add(new CustomerSession
        {
            CustomerId = customerId,
            TokenHash = hash,
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim(),
            CreatedAt = now,
            FamilyCreatedAt = familyCreatedAt,
            LastUsedAt = now,
            ExpiresAt = expires < absolute ? expires : absolute
        });
    }
}

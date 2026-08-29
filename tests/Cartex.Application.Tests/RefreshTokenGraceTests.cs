using Cartex.Application.Auth;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Tests.Common;
using Cartex.Auth.Services;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class RefreshTokenGraceTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task WP11_reused_refresh_token_revokes_family_only_after_120_seconds()
    {
        const string rawToken = "reused-refresh-token";
        long userId;
        long activeSessionId;
        using (var arrange = Fixture.CreateScope())
        {
            var db = arrange.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            userId = await db.Users.Select(user => user.Id).FirstAsync();
            var now = DateTime.UtcNow;
            db.RefreshSessions.Add(new RefreshSession
            {
                UserId = userId,
                TokenHash = RefreshTokens.Hash(rawToken),
                CreatedAt = now.AddDays(-1),
                FamilyCreatedAt = now.AddDays(-1),
                LastUsedAt = now.AddMinutes(-1),
                ExpiresAt = now.AddDays(1),
                RevokedAt = now.AddSeconds(-60),
                ReplacedByHash = "replacement"
            });
            var active = new RefreshSession
            {
                UserId = userId,
                TokenHash = "active-session",
                CreatedAt = now,
                FamilyCreatedAt = now.AddDays(-1),
                LastUsedAt = now,
                ExpiresAt = now.AddDays(1)
            };
            db.RefreshSessions.Add(active);
            await db.SaveChangesAsync();
            activeSessionId = active.Id;
        }

        using (var withinGrace = Fixture.CreateScope())
        {
            var builder = CreateBuilder(withinGrace);
            Assert.Null(await builder.RotateAsync(rawToken, null, null, CancellationToken.None));
            var db = withinGrace.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null((await db.RefreshSessions.FindAsync(activeSessionId))!.RevokedAt);
        }

        using (var ageSession = Fixture.CreateScope())
        {
            var db = ageSession.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reused = await db.RefreshSessions.SingleAsync(session => session.TokenHash == RefreshTokens.Hash(rawToken));
            reused.RevokedAt = DateTime.UtcNow.AddSeconds(-121);
            await db.SaveChangesAsync();
        }

        using (var pastGrace = Fixture.CreateScope())
        {
            var builder = CreateBuilder(pastGrace);
            Assert.Null(await builder.RotateAsync(rawToken, null, null, CancellationToken.None));
            var db = pastGrace.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.NotNull((await db.RefreshSessions.FindAsync(activeSessionId))!.RevokedAt);
        }
    }

    private AuthTokenBuilder CreateBuilder(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
        new UnusedJwtTokenGenerator(),
        new UnusedLicenseService(),
        scope.ServiceProvider.GetRequiredService<IAuditService>(),
        Fixture.CurrentUser);

    private sealed class UnusedJwtTokenGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(
            long userId,
            string username,
            string fullName,
            IEnumerable<string> roles,
            string? startPage,
            IEnumerable<string> permissions,
            string authorizationStamp,
            long businessId,
            IEnumerable<long> branchIds,
            long? defaultBranchId,
            string? deviceId) => throw new NotSupportedException();

        public string GenerateCustomerToken(long customerId, string fullName) => throw new NotSupportedException();
    }

    private sealed class UnusedLicenseService : ILicenseService
    {
        public Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsActiveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetTariffFeaturesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Invalidate() => throw new NotSupportedException();
    }
}

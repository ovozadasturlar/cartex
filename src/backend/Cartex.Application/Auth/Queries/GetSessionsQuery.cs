using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Queries;

public record DeviceSessionDto(long Id, string? DeviceName, DateTime CreatedAt, DateTime LastUsedAt, DateTime ExpiresAt, string? Username = null);

public record GetSessionsQuery(bool All = false) : IRequest<IReadOnlyList<DeviceSessionDto>>;

public sealed class GetSessionsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSessionsQuery, IReadOnlyList<DeviceSessionDto>>
{
    public async Task<IReadOnlyList<DeviceSessionDto>> Handle(GetSessionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var all = request.All && currentUser.HasPermission(AppPermissions.Users.Manage);
        var now = DateTime.UtcNow;
        var sessions = await db.RefreshSessions
            .Where(s => (all || s.UserId == userId) && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastUsedAt)
            .Select(s => new { s.Id, s.UserId, s.DeviceName, s.CreatedAt, s.LastUsedAt, s.ExpiresAt, s.User.Username })
            .ToListAsync(cancellationToken);

        return sessions
            .GroupBy(s => new { s.UserId, s.DeviceName })
            .Select(g => g.First())
            .Select(s => new DeviceSessionDto(s.Id, s.DeviceName, s.CreatedAt, s.LastUsedAt, s.ExpiresAt, all ? s.Username : null))
            .OrderByDescending(s => s.LastUsedAt)
            .ToList();
    }
}

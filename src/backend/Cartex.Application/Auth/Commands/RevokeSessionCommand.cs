using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record RevokeSessionCommand(long Id) : IRequest<Unit>;

public sealed class RevokeSessionCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<RevokeSessionCommand, Unit>
{
    public async Task<Unit> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var canManageAll = currentUser.HasPermission(AppPermissions.Devices.ViewAll);
        var now = DateTime.UtcNow;
        var target = await db.RefreshSessions
            .Where(s => s.Id == request.Id && (canManageAll || s.UserId == userId) && s.RevokedAt == null)
            .Select(s => new { s.UserId, s.DeviceName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Session not found.");

        await db.RefreshSessions
            .Where(s => s.UserId == target.UserId && s.DeviceName == target.DeviceName && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);

        return Unit.Value;
    }
}

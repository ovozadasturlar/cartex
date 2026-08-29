using Cartex.Application.Common.Security;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Roles.Commands;

public record SetRoleActiveCommand(long RoleId, bool IsActive) : ICommand<Unit>;

public sealed class SetRoleActiveCommandHandler(
    IApplicationDbContext db,
    IAccessControlService accessControl,
    IAuditService audit) : IRequestHandler<SetRoleActiveCommand, Unit>
{
    public async Task<Unit> Handle(SetRoleActiveCommand request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role not found.");

        await accessControl.EnsureCanManageRoleAsync(role, cancellationToken);

        if (role.AccessAll && !request.IsActive)
            throw new BusinessRuleException("The system owner role cannot be deactivated.");

        if (role.IsActive == request.IsActive)
            return Unit.Value;

        role.IsActive = request.IsActive;
        audit.Add(
            request.IsActive ? "role.activate" : "role.deactivate",
            "roles",
            role.Id,
            new { role.Name, request.IsActive });
        await db.SaveChangesAsync(cancellationToken);

        var affectedUserIds = db.UserRoles
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => ur.UserId);
        var now = DateTime.UtcNow;
        await db.RefreshSessions
            .Where(session => affectedUserIds.Contains(session.UserId) && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.RevokedAt, now),
                cancellationToken);

        return Unit.Value;
    }
}

using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Application.Common.Security;

namespace Cartex.Application.Roles.Commands;

public record DeleteRoleCommand(long Id) : ICommand<Unit>;

public sealed class DeleteRoleCommandHandler(IApplicationDbContext db, IAccessControlService accessControl) : IRequestHandler<DeleteRoleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Role not found.");

        await accessControl.EnsureCanManageRoleAsync(role, cancellationToken);

        if (role.IsSystem)
            throw new BusinessRuleException("System roles cannot be deleted.");

        var inUse = await db.Users.AnyAsync(u => u.UserRoles.Any(ur => ur.RoleId == role.Id), cancellationToken);
        if (inUse)
            throw new BusinessRuleException("Role is assigned to users.");

        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

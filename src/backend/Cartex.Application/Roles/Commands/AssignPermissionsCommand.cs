using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Security;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Roles.Commands;

public record AssignPermissionsCommand(long RoleId, List<long> PermissionIds) : ICommand<Unit>;

public sealed class AssignPermissionsCommandHandler(IApplicationDbContext db, IAccessControlService accessControl) : IRequestHandler<AssignPermissionsCommand, Unit>
{
    public async Task<Unit> Handle(AssignPermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role not found.");

        await accessControl.EnsureCanManageRoleAsync(role, cancellationToken);
        await accessControl.EnsureCanGrantAsync(request.PermissionIds, cancellationToken);

        var existing = await db.RolePermissions
            .Where(rp => rp.RoleId == request.RoleId)
            .ToListAsync(cancellationToken);

        db.RolePermissions.RemoveRange(existing);

        var requestedNames = await db.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        var requiredNames = requestedNames.Concat(PermissionDependencies.RequiredFor(requestedNames)).ToHashSet();

        var enabledPermissionIds = await db.Permissions
            .Where(p => requiredNames.Contains(p.Name) && p.IsEnabled)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in enabledPermissionIds)
        {
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = request.RoleId,
                PermissionId = permissionId
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Security;
using FluentValidation;

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

        var requested = await db.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        if (requested.Count != request.PermissionIds.Distinct().Count())
            throw new ValidationException("One or more permissions do not exist.");

        var effectiveNames = PermissionDependencies.Effective(requested.Select(p => p.Name));
        var effectivePermissionIds = await db.Permissions
            .Where(p => effectiveNames.Contains(p.Name) && p.IsEnabled)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        if (effectivePermissionIds.Count != effectiveNames.Count)
            throw new ValidationException("A required permission is disabled or missing.");

        await accessControl.EnsureCanGrantAsync(effectivePermissionIds, cancellationToken);

        var existing = await db.RolePermissions
            .Where(rp => rp.RoleId == request.RoleId)
            .ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(existing);

        foreach (var permissionId in effectivePermissionIds)
        {
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = request.RoleId,
                PermissionId = permissionId
            });
        }

        role.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Roles.Commands;

public record AssignPermissionsCommand(long RoleId, List<long> PermissionIds) : IRequest<MediatR.Unit>;

public sealed class AssignPermissionsCommandHandler(IApplicationDbContext db) : IRequestHandler<AssignPermissionsCommand, MediatR.Unit>
{
    public async Task<MediatR.Unit> Handle(AssignPermissionsCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.RolePermissions
            .Where(rp => rp.RoleId == request.RoleId)
            .ToListAsync(cancellationToken);

        db.RolePermissions.RemoveRange(existing);

        var enabledPermissionIds = await db.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id) && p.IsEnabled)
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

        return MediatR.Unit.Value;
    }
}

using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Authorization;

namespace Cartex.Application.Permissions.Commands;

public record TogglePermissionCommand(long Id, bool IsEnabled) : ICommand<Unit>;

public sealed class TogglePermissionCommandHandler(IApplicationDbContext db) : IRequestHandler<TogglePermissionCommand, Unit>
{
    public async Task<Unit> Handle(TogglePermissionCommand request, CancellationToken cancellationToken)
    {
        var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Permission not found.");

        permission.IsEnabled = request.IsEnabled;
        if (request.IsEnabled)
        {
            var dependencies = PermissionDependencies.RequiredFor([permission.Name]);
            var requiredPermissions = await db.Permissions
                .Where(p => dependencies.Contains(p.Name) && !p.IsEnabled)
                .ToListAsync(cancellationToken);
            foreach (var required in requiredPermissions)
                required.IsEnabled = true;
        }
        else
        {
            var dependents = PermissionDependencies.DependentsOf(permission.Name);
            var dependentPermissions = await db.Permissions
                .Where(p => dependents.Contains(p.Name) && p.IsEnabled)
                .ToListAsync(cancellationToken);
            foreach (var dependent in dependentPermissions)
                dependent.IsEnabled = false;
        }
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Permissions.Commands;

public record TogglePermissionCommand(long Id, bool IsEnabled) : ICommand<Unit>;

public sealed class TogglePermissionCommandHandler(IApplicationDbContext db) : IRequestHandler<TogglePermissionCommand, Unit>
{
    public async Task<Unit> Handle(TogglePermissionCommand request, CancellationToken cancellationToken)
    {
        var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Permission not found.");

        permission.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

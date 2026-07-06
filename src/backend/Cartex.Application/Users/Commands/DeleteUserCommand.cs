using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Security;

namespace Cartex.Application.Users.Commands;

public record DeleteUserCommand(long Id) : ICommand<Unit>;

public sealed class DeleteUserCommandHandler(IApplicationDbContext db, IAccessControlService accessControl, IAuditService audit) : IRequestHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        await accessControl.EnsureCanManageUserAsync(user, cancellationToken);

        if (user.UserRoles.Any(ur => ur.Role.AccessAll))
            throw new BusinessRuleException("This user cannot be deleted.");

        if (user.UserRoles.Any(ur => ur.Role.Level >= AppRoles.AdminLevel && !ur.Role.AccessAll))
            await accessControl.EnsureAdminRemainsAsync(user.Id, cancellationToken);

        audit.Add("user.delete", "users", user.Id, new { user.Username, user.FullName });

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

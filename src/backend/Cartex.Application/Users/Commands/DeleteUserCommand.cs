using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Application.Common.Security;

namespace Cartex.Application.Users.Commands;

public record DeleteUserCommand(long Id) : ICommand<Unit>;

public sealed class DeleteUserCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAccessControlService accessControl, IAuditService audit) : IRequestHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        await accessControl.EnsureCanManageUserAsync(user, cancellationToken);

        if (currentUser.UserId == user.Id)
            throw new BusinessRuleException("You cannot delete your own user.");

        if (user.UserRoles.Any(ur => ur.Role.AccessAll))
            throw new BusinessRuleException("This user cannot be deleted.");

        if (user.UserRoles.Any(ur => ur.Role.Level >= AppRoles.AdminLevel && !ur.Role.AccessAll))
            await accessControl.EnsureAdminRemainsAsync(user.Id, cancellationToken);

        audit.Add("user.delete", "users", user.Id, new { user.Username, user.FullName });

        await db.RefreshSessions.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Auth.Services;
using Cartex.Domain.Entities;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Security;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Users.Commands;

public record UpdateUserCommand(
    long Id, string FullName, List<long> RoleIds, bool IsActive, string? NewPassword,
    long? DefaultBranchId, List<long> BranchIds, string? StartPage) : ICommand<Unit>;

public sealed class UpdateUserCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IAccessControlService accessControl,
    IAuditService audit) : IRequestHandler<UpdateUserCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.UserBranches)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        await accessControl.EnsureCanManageUserAsync(user, cancellationToken);
        await accessControl.EnsureCanAssignRolesAsync(request.RoleIds, cancellationToken);

        var wasAdmin = user.UserRoles.Any(ur => ur.Role.Level >= AppRoles.AdminLevel && !ur.Role.AccessAll);
        var willBeAdmin = request.IsActive && await db.Roles
            .AnyAsync(r => request.RoleIds.Contains(r.Id) && r.Level >= AppRoles.AdminLevel && !r.AccessAll, cancellationToken);
        if (wasAdmin && !willBeAdmin)
            await accessControl.EnsureAdminRemainsAsync(user.Id, cancellationToken);

        user.FullName = request.FullName;
        user.IsActive = request.IsActive;
        user.DefaultBranchId = request.DefaultBranchId;
        user.StartPage = request.StartPage;

        if (request.NewPassword is not null)
            user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        user.UserRoles.Clear();
        foreach (var roleId in request.RoleIds.Distinct())
            user.UserRoles.Add(new UserRole { RoleId = roleId });

        user.UserBranches.Clear();
        foreach (var branchId in request.BranchIds.Distinct())
            user.UserBranches.Add(new UserBranch { BranchId = branchId });

        audit.Add("user.update", "users", user.Id, new { user.Username, request.FullName, request.IsActive, request.RoleIds });

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.RoleIds).NotEmpty();
        RuleFor(x => x.NewPassword).MinimumLength(6).When(x => x.NewPassword is not null);
    }
}

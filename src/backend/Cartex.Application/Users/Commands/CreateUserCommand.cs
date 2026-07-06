using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Auth.Services;
using Cartex.Application.Common.Security;

namespace Cartex.Application.Users.Commands;

public record CreateUserCommand(
    string FullName, string Username, string Password, List<long> RoleIds,
    long? DefaultBranchId, List<long> BranchIds, string? StartPage) : ICommand<long>;

public sealed class CreateUserCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IAccessControlService accessControl,
    IAuditService audit) : IRequestHandler<CreateUserCommand, long>
{
    public async Task<long> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        await accessControl.EnsureCanAssignRolesAsync(request.RoleIds, cancellationToken);

        var user = new User
        {
            FullName = request.FullName,
            Username = request.Username,
            PasswordHash = passwordHasher.Hash(request.Password),
            DefaultBranchId = request.DefaultBranchId,
            StartPage = request.StartPage,
            UserRoles = [.. request.RoleIds.Distinct().Select(id => new UserRole { RoleId = id })],
            UserBranches = [.. request.BranchIds.Distinct().Select(id => new UserBranch { BranchId = id })]
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        audit.Add("user.create", "users", user.Id, new { user.Username, user.FullName, request.RoleIds });
        await db.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.RoleIds).NotEmpty();
    }
}

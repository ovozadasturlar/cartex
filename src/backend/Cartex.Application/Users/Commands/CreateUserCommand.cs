using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Auth.Services;

namespace Cartex.Application.Users.Commands;

public record CreateUserCommand(
    string FullName, string Username, string Password, long RoleId,
    long? DefaultBranchId, List<long> BranchIds) : IRequest<long>;

public sealed class CreateUserCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher) : IRequestHandler<CreateUserCommand, long>
{
    public async Task<long> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            FullName = request.FullName,
            Username = request.Username,
            PasswordHash = passwordHasher.Hash(request.Password),
            RoleId = request.RoleId,
            DefaultBranchId = request.DefaultBranchId,
            UserBranches = [.. request.BranchIds.Distinct().Select(id => new UserBranch { BranchId = id })]
        };

        db.Users.Add(user);
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
    }
}

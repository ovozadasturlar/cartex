using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Auth.Services;

namespace Cartex.Application.Users.Commands;

public record CreateUserCommand(long ShopId, string FullName, string Username, string Password, long RoleId) : IRequest<long>;

public sealed class CreateUserCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher) : IRequestHandler<CreateUserCommand, long>
{
    public async Task<long> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            ShopId = request.ShopId,
            FullName = request.FullName,
            Username = request.Username,
            PasswordHash = passwordHasher.Hash(request.Password),
            RoleId = request.RoleId
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

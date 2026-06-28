using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Roles.Commands;

public record CreateRoleCommand(string Name, string? Description) : ICommand<long>;

public sealed class CreateRoleCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateRoleCommand, long>
{
    public async Task<long> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = new Role
        {
            Name = request.Name,
            Description = request.Description
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(30);
    }
}

using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Application.Common.Security;

namespace Cartex.Application.Roles.Commands;

public record CreateRoleCommand(string Name, string? Description, string? StartPage, int Priority, List<string>? GrantablePermissions = null, List<string>? AssignableRoles = null, string? CartDestination = null) : ICommand<long>;

public sealed class CreateRoleCommandHandler(IApplicationDbContext db, IAccessControlService accessControl) : IRequestHandler<CreateRoleCommand, long>
{
    public async Task<long> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        await accessControl.EnsureCanCreateRoleAsync(request.Priority, cancellationToken);

        var grantable = request.GrantablePermissions ?? [];
        await accessControl.EnsureCanDelegateAsync(grantable, cancellationToken);

        var role = new Role
        {
            Name = request.Name,
            Description = request.Description,
            StartPage = request.StartPage,
            CartDestination = request.CartDestination,
            Priority = request.Priority,
            Level = request.Priority,
            GrantablePermissions = grantable,
            AssignableRoles = request.AssignableRoles ?? []
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
        RuleFor(x => x.CartDestination).Must(v => v is null or "queue" or "order");
    }
}

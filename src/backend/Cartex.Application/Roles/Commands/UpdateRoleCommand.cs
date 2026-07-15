using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Application.Common.Security;

namespace Cartex.Application.Roles.Commands;

public record UpdateRoleCommand(long Id, string Name, string? Description, string? StartPage, int Priority, List<string>? GrantablePermissions = null, List<string>? AssignableRoles = null, string? CartDestination = null) : ICommand<Unit>;

public sealed class UpdateRoleCommandHandler(IApplicationDbContext db, IAccessControlService accessControl) : IRequestHandler<UpdateRoleCommand, Unit>
{
    public async Task<Unit> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Role not found.");

        await accessControl.EnsureCanManageRoleAsync(role, cancellationToken);

        role.Description = request.Description;
        role.StartPage = request.StartPage;
        role.CartDestination = request.CartDestination;
        role.Priority = request.Priority;

        if (request.AssignableRoles is not null)
            role.AssignableRoles = request.AssignableRoles;

        if (request.GrantablePermissions is not null)
        {
            await accessControl.EnsureCanDelegateAsync(request.GrantablePermissions, cancellationToken);
            role.GrantablePermissions = request.GrantablePermissions;
        }

        if (!role.IsSystem)
        {
            await accessControl.EnsureCanCreateRoleAsync(request.Priority, cancellationToken);
            role.Name = request.Name;
            role.Level = request.Priority;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CartDestination).Must(v => v is null or "queue" or "order");
    }
}

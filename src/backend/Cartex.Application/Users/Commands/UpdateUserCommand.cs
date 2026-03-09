using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Auth.Services;

namespace Cartex.Application.Users.Commands;

public record UpdateUserCommand(long Id, string FullName, long ShopId, long RoleId, bool IsActive, string? NewPassword) : IRequest<Unit>;

public sealed class UpdateUserCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher) : IRequestHandler<UpdateUserCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new Exception("User not found.");

        user.FullName = request.FullName;
        user.ShopId = request.ShopId;
        user.RoleId = request.RoleId;
        user.IsActive = request.IsActive;

        if (request.NewPassword is not null)
            user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

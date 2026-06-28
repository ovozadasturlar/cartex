using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Auth.Services;
using Cartex.Domain.Entities;
using Unit = MediatR.Unit;

namespace Cartex.Application.Users.Commands;

public record UpdateUserCommand(
    long Id, string FullName, long RoleId, bool IsActive, string? NewPassword,
    long? DefaultBranchId, List<long> BranchIds) : IRequest<Unit>;

public sealed class UpdateUserCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher) : IRequestHandler<UpdateUserCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        user.FullName = request.FullName;
        user.RoleId = request.RoleId;
        user.IsActive = request.IsActive;
        user.DefaultBranchId = request.DefaultBranchId;

        if (request.NewPassword is not null)
            user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        user.UserBranches.Clear();
        foreach (var branchId in request.BranchIds.Distinct())
            user.UserBranches.Add(new UserBranch { BranchId = branchId });

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

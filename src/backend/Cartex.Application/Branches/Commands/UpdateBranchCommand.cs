using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Branches.Commands;

public record UpdateBranchCommand(long Id, string Name, string? Address, string? Phone, bool IsActive) : ICommand<Unit>;

public sealed class UpdateBranchCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateBranchCommand, Unit>
{
    public async Task<Unit> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Branch not found.");

        branch.Name = request.Name;
        branch.Address = request.Address;
        branch.Phone = request.Phone;
        branch.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

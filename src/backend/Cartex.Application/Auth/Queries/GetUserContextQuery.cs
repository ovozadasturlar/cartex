using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Auth;

namespace Cartex.Application.Auth.Queries;

public record GetUserContextQuery : IRequest<UserContextDto>;

public sealed class GetUserContextQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetUserContextQuery, UserContextDto>
{
    public async Task<UserContextDto> Handle(GetUserContextQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();

        var branchesQuery = db.Branches.AsNoTracking();
        if (!currentUser.CanAccessAllBranches)
            branchesQuery = branchesQuery.Where(b => currentUser.BranchIds.Contains(b.Id));

        var branches = await branchesQuery
            .OrderByDescending(b => b.Id == currentUser.DefaultBranchId)
            .ThenBy(b => b.Name)
            .Select(b => new UserContextBranchDto(b.Id, b.Name, b.IsActive))
            .ToListAsync(cancellationToken);

        var branchIds = branches.Select(b => b.Id).ToList();
        var warehouses = await db.Warehouses
            .AsNoTracking()
            .Where(w => branchIds.Contains(w.BranchId))
            .OrderBy(w => w.Name)
            .Select(w => new UserContextWarehouseDto(w.Id, w.Name, w.BranchId, w.Branch.Name))
            .ToListAsync(cancellationToken);

        return new UserContextDto(currentUser.DefaultBranchId, branches, warehouses);
    }
}

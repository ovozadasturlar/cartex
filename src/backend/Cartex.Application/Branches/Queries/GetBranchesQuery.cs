using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;

namespace Cartex.Application.Branches.Queries;

public record GetBranchesQuery : FilteringRequest, IRequest<IReadOnlyCollection<BranchDto>>;

public record BranchDto(long Id, string Name, string? Address, string? Phone, bool IsActive);

public sealed class GetBranchesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetBranchesQuery, IReadOnlyCollection<BranchDto>>
{
    public async Task<IReadOnlyCollection<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        return await db.Branches
            .ToPagedListAsync(request,
                b => new BranchDto(b.Id, b.Name, b.Address, b.Phone, b.IsActive),
                writer, cancellationToken);
    }
}

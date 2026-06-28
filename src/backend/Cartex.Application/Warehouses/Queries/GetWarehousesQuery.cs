using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Warehouses.Queries;

public record GetWarehousesQuery : FilteringRequest, IRequest<IReadOnlyCollection<WarehouseDto>>;

public record WarehouseDto(long Id, string Name, long BranchId, string BranchName);

public sealed class GetWarehousesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetWarehousesQuery, IReadOnlyCollection<WarehouseDto>>
{
    public async Task<IReadOnlyCollection<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        return await db.Warehouses
            .Include(w => w.Branch)
            .ToPagedListAsync(request,
                w => new WarehouseDto(w.Id, w.Name, w.BranchId, w.Branch.Name),
                writer, cancellationToken);
    }
}

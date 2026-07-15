using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Warehouses.Queries;

public record GetWarehousesQuery : FilteringRequest, IRequest<IReadOnlyCollection<WarehouseDto>>;

public record WarehouseDto(long Id, string Name, long BranchId, string BranchName, bool IsOnline = false, long? AssignedUserId = null);

public sealed class GetWarehousesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetWarehousesQuery, IReadOnlyCollection<WarehouseDto>>
{
    public async Task<IReadOnlyCollection<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        return await db.Warehouses
            .Include(w => w.Branch)
            .ToPagedListAsync(request,
                w => new WarehouseDto(w.Id, w.Name, w.BranchId, w.Branch.Name, w.IsOnline, w.AssignedUserId),
                writer, cancellationToken);
    }
}

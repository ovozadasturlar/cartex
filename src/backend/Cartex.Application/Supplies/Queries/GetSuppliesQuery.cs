using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Supplies.Queries;

public record GetSuppliesQuery : FilteringRequest, IRequest<IReadOnlyCollection<SupplyDto>>;

public record SupplyDto(long Id, DateOnly SupplyDate, decimal TotalAmount, string SupplierName, string WarehouseName, string UserName);

public sealed class GetSuppliesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetSuppliesQuery, IReadOnlyCollection<SupplyDto>>
{
    public async Task<IReadOnlyCollection<SupplyDto>> Handle(GetSuppliesQuery request, CancellationToken cancellationToken)
    {
        return await db.Supplies
            .Include(s => s.Supplier)
            .Include(s => s.Warehouse)
            .Include(s => s.User)
            .ToPagedListAsync(request,
                s => new SupplyDto(
                    s.Id,
                    s.SupplyDate,
                    s.TotalAmount,
                    s.Supplier.Name,
                    s.Warehouse.Name,
                    s.User.FullName),
                writer, cancellationToken);
    }
}

using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;

namespace Cartex.Application.Supplies.Queries;

public record GetSuppliesQuery : FilteringRequest, IRequest<IReadOnlyCollection<SupplyDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? SupplierId { get; set; }
}

public record SupplyDto(long Id, DateOnly SupplyDate, decimal TotalAmount, string SupplierName, string WarehouseName, string UserName);

public sealed class GetSuppliesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetSuppliesQuery, IReadOnlyCollection<SupplyDto>>
{
    public async Task<IReadOnlyCollection<SupplyDto>> Handle(GetSuppliesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Supplies.AsQueryable();

        if (request.FromDate is { } fromDate)
            query = query.Where(s => s.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(s => s.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (request.SupplierId is { } supplierId)
            query = query.Where(s => s.SupplierId == supplierId);

        return await query
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

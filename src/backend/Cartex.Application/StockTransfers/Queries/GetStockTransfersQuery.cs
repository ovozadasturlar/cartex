using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;

namespace Cartex.Application.StockTransfers.Queries;

public record GetStockTransfersQuery : FilteringRequest, IRequest<IReadOnlyCollection<StockTransferDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? WarehouseId { get; set; }
    public long? ToWarehouseId { get; set; }
}

public record StockTransferDto(
    long Id,
    string ProductName,
    decimal Quantity,
    string FromWarehouse,
    string ToWarehouse,
    string Status,
    DateTime CreatedAt,
    string UserName);

public sealed class GetStockTransfersQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetStockTransfersQuery, IReadOnlyCollection<StockTransferDto>>
{
    public async Task<IReadOnlyCollection<StockTransferDto>> Handle(GetStockTransfersQuery request, CancellationToken cancellationToken)
    {
        var query = db.StockTransfers.AsQueryable();

        if (request.FromDate is { } fromDate)
            query = query.Where(t => t.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(t => t.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (request.WarehouseId is { } warehouseId)
            query = query.Where(t => t.FromWarehouseId == warehouseId);
        if (request.ToWarehouseId is { } toWarehouseId)
            query = query.Where(t => t.ToWarehouseId == toWarehouseId);

        return await query
            .ToPagedListAsync(request,
                t => new StockTransferDto(
                    t.Id,
                    t.Variant.Product.Name,
                    t.Quantity,
                    t.FromWarehouse.Name,
                    t.ToWarehouse.Name,
                    t.Status.ToString(),
                    t.CreatedAt,
                    t.User.FullName),
                writer, cancellationToken);
    }
}

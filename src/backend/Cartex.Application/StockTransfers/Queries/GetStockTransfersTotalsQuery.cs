using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.StockTransfers;

namespace Cartex.Application.StockTransfers.Queries;

public record GetStockTransfersTotalsQuery : FilteringRequest, IRequest<StockTransfersTotalsDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? WarehouseId { get; set; }
}

public sealed class GetStockTransfersTotalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetStockTransfersTotalsQuery, StockTransfersTotalsDto>
{
    public async Task<StockTransfersTotalsDto> Handle(GetStockTransfersTotalsQuery request, CancellationToken cancellationToken)
    {
        var query = db.StockTransfers.AsQueryable();

        if (request.FromDate is { } fromDate)
            query = query.Where(t => t.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(t => t.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (request.WarehouseId is { } warehouseId)
            query = query.Where(t => t.FromWarehouseId == warehouseId);

        query = query.AsFilterable(request);

        return new StockTransfersTotalsDto(
            await query.CountAsync(cancellationToken),
            await query.SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0);
    }
}

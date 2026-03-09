using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.StockTransfers.Queries;

public record GetStockTransfersQuery : FilteringRequest, IRequest<IReadOnlyCollection<StockTransferDto>>;

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
        return await db.StockTransfers
            .Include(t => t.Product)
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.User)
            .Select(t => new StockTransferDto(
                t.Id,
                t.Product.Name,
                t.Quantity,
                t.FromWarehouse.Name,
                t.ToWarehouse.Name,
                t.Status.ToString(),
                t.CreatedAt,
                t.User.FullName))
            .ToPagedListAsync(request, writer, cancellationToken);
    }
}

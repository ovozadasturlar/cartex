using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.StockTransfers.Commands;

public record ReceiveStockTransferCommand(long Id) : IRequest<MediatR.Unit>;

public sealed class ReceiveStockTransferCommandHandler(IApplicationDbContext db) : IRequestHandler<ReceiveStockTransferCommand, MediatR.Unit>
{
    public async Task<MediatR.Unit> Handle(ReceiveStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await db.StockTransfers
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new Exception("Transfer not found.");

        if (transfer.Status != TransferStatus.Sent)
            throw new Exception("Transfer is not in Sent status.");

        transfer.Status = TransferStatus.Received;

        var sourceStocks = await db.Stocks
            .Where(s => s.WarehouseId == transfer.FromWarehouseId && s.ProductId == transfer.ProductId && s.Quantity > 0)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var remaining = transfer.Quantity;

        foreach (var stock in sourceStocks)
        {
            if (remaining <= 0) break;

            var deduct = Math.Min(stock.Quantity, remaining);
            stock.Quantity -= deduct;
            remaining -= deduct;

            var targetStock = await db.Stocks
                .FirstOrDefaultAsync(s =>
                    s.WarehouseId == transfer.ToWarehouseId &&
                    s.ProductId == transfer.ProductId &&
                    s.PurchasePrice == stock.PurchasePrice, cancellationToken);

            if (targetStock is not null)
            {
                targetStock.Quantity += deduct;
            }
            else
            {
                db.Stocks.Add(new Stock
                {
                    ProductId = transfer.ProductId,
                    WarehouseId = transfer.ToWarehouseId,
                    Quantity = deduct,
                    PurchasePrice = stock.PurchasePrice,
                    SellingPrice = stock.SellingPrice
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return MediatR.Unit.Value;
    }
}

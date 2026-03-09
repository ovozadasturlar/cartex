using MediatR;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Supplies.Commands;

public record CreateSupplyItemDto(long ProductId, decimal Quantity, decimal PurchasePrice);

public record CreateSupplyCommand(
    long SupplierId,
    long WarehouseId,
    long UserId,
    DateOnly SupplyDate,
    List<CreateSupplyItemDto> Items) : IRequest<long>;

public sealed class CreateSupplyCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateSupplyCommand, long>
{
    public async Task<long> Handle(CreateSupplyCommand request, CancellationToken cancellationToken)
    {
        var totalAmount = request.Items.Sum(i => i.Quantity * i.PurchasePrice);

        var supply = new Supply
        {
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            UserId = request.UserId,
            SupplyDate = request.SupplyDate,
            TotalAmount = totalAmount
        };

        db.Supplies.Add(supply);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            db.SupplyItems.Add(new SupplyItem
            {
                SupplyId = supply.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                PurchasePrice = item.PurchasePrice
            });

            db.Stocks.Add(new Stock
            {
                ProductId = item.ProductId,
                WarehouseId = request.WarehouseId,
                Quantity = item.Quantity,
                PurchasePrice = item.PurchasePrice,
                SellingPrice = 0
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return supply.Id;
    }
}

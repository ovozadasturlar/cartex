using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;

namespace Cartex.Application.Supplies.Commands;

public record CreateSupplyItemDto(long ProductId, decimal Quantity, decimal PurchasePrice, DateOnly? ExpiredAt);

public record CreateSupplyCommand(
    long SupplierId,
    long WarehouseId,
    DateOnly SupplyDate,
    List<CreateSupplyItemDto> Items) : ICommand<long>;

public sealed class CreateSupplyCommandHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<CreateSupplyCommand, long>
{
    public async Task<long> Handle(CreateSupplyCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var supply = new Supply
        {
            BranchId = warehouse.BranchId,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            UserId = userId,
            SupplyDate = request.SupplyDate,
            TotalAmount = request.Items.Sum(i => i.Quantity * i.PurchasePrice)
        };

        foreach (var item in request.Items)
        {
            supply.Items.Add(new SupplyItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                PurchasePrice = item.PurchasePrice
            });

            db.Stocks.Add(new Stock
            {
                BranchId = warehouse.BranchId,
                ProductId = item.ProductId,
                WarehouseId = request.WarehouseId,
                Supply = supply,
                Quantity = item.Quantity,
                PurchasePrice = item.PurchasePrice,
                ExpiredAt = item.ExpiredAt
            });
        }

        db.Supplies.Add(supply);
        await db.SaveChangesAsync(cancellationToken);

        return supply.Id;
    }
}

using Cartex.Application.CustomerReturns.Commands;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Tests.Common;

internal static class TestReturns
{
    public static CustomerReturnLineInput Line(SaleItem item, decimal? quantity = null, bool restock = true) =>
        new(item.VariantId,
            quantity ?? item.Quantity - item.ReturnedQuantity,
            item.Id,
            null,
            null,
            restock ? ReturnItemCondition.Sellable : ReturnItemCondition.Opened,
            restock ? InventoryDisposition.SellableRestock : InventoryDisposition.Quarantine);

    public static async Task<CreateCustomerReturnCommand> ForSaleAsync(
        ApplicationDbContext db,
        long saleId,
        decimal? quantity = null,
        bool restock = true)
    {
        var sale = await db.Sales.AsNoTracking().FirstAsync(x => x.Id == saleId);
        var items = await db.SaleItems.AsNoTracking().Where(x => x.SaleId == saleId).ToListAsync();
        return new CreateCustomerReturnCommand(
            sale.WarehouseId,
            [.. items.Select(x => Line(x, quantity, restock))],
            sale.CustomerId);
    }

    public static async Task<CreateCustomerReturnCommand> ForItemAsync(
        ApplicationDbContext db,
        long saleItemId,
        decimal quantity,
        bool restock = true)
    {
        var item = await db.SaleItems.AsNoTracking().FirstAsync(x => x.Id == saleItemId);
        var sale = await db.Sales.AsNoTracking().FirstAsync(x => x.Id == item.SaleId);
        return new CreateCustomerReturnCommand(
            sale.WarehouseId,
            [Line(item, quantity, restock)],
            sale.CustomerId);
    }
}

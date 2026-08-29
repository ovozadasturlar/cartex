using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Products;

public static class ProductPriceWriter
{
    public static async Task UpsertAsync(IApplicationDbContext db, long variantId, long? warehouseId, decimal sellingPrice, CancellationToken cancellationToken, string? currency = null)
    {
        var price = db.ProductPrices.Local.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == warehouseId)
            ?? await db.ProductPrices.FirstOrDefaultAsync(p => p.VariantId == variantId && p.WarehouseId == warehouseId, cancellationToken);

        var code = currency ?? price?.Currency ?? await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);

        if (price is null)
            db.ProductPrices.Add(new ProductPrice { VariantId = variantId, WarehouseId = warehouseId, SellingPrice = sellingPrice, Currency = code });
        else
        {
            price.SellingPrice = sellingPrice;
            price.Currency = code;
        }
    }
}

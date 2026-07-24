using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductByBarcodeQuery(string Code, long WarehouseId, bool ForSale = false) : IRequest<ProductLookupDto?>;

public record ProductLookupDto(long VariantId, string ProductName, string UnitName, decimal PackQty, decimal SellingPrice, decimal OnHand, string Dimension, string? ImageKey = null);

public sealed class GetProductByBarcodeQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductByBarcodeQuery, ProductLookupDto?>
{
    public async Task<ProductLookupDto?> Handle(GetProductByBarcodeQuery request, CancellationToken cancellationToken)
    {
        var barcode = await db.Barcodes
            .Where(b => b.Code == request.Code)
            .Select(b => new { b.VariantId, b.PackQty, ProductName = b.Variant.Product.Name, UnitName = b.Variant.Product.Unit.Name, Dimension = b.Variant.Product.Unit.Dimension, b.Variant.Product.IsEnabled, ImageKey = b.Variant.ImageKey ?? b.Variant.Product.ImageKey })
            .FirstOrDefaultAsync(cancellationToken);

        barcode ??= await db.ProductVariants
            .Where(v => v.Code == request.Code)
            .Select(v => new { VariantId = v.Id, PackQty = 1m, ProductName = v.Product.Name, UnitName = v.Product.Unit.Name, Dimension = v.Product.Unit.Dimension, v.Product.IsEnabled, ImageKey = v.ImageKey ?? v.Product.ImageKey })
            .FirstOrDefaultAsync(cancellationToken);

        if (barcode is null || (request.ForSale && !barcode.IsEnabled))
            return null;

        var prices = await db.ProductPrices
            .Where(pp => pp.VariantId == barcode.VariantId && (pp.WarehouseId == request.WarehouseId || pp.WarehouseId == null))
            .ToListAsync(cancellationToken);

        var sellingPrice = (prices.FirstOrDefault(p => p.WarehouseId == request.WarehouseId)
            ?? prices.FirstOrDefault(p => p.WarehouseId == null))?.SellingPrice ?? 0;

        var onHand = await db.Stocks
            .Where(s => s.VariantId == barcode.VariantId && s.WarehouseId == request.WarehouseId)
            .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0;

        return new ProductLookupDto(barcode.VariantId, barcode.ProductName, barcode.UnitName, barcode.PackQty, sellingPrice, onHand, barcode.Dimension.ToString(), barcode.ImageKey);
    }
}

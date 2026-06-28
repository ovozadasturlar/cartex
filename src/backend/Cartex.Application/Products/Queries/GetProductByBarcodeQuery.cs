using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductByBarcodeQuery(string Code, long WarehouseId) : IRequest<ProductLookupDto?>;

public record ProductLookupDto(long ProductId, string ProductName, string UnitName, decimal PackQty, decimal SellingPrice, decimal OnHand);

public sealed class GetProductByBarcodeQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductByBarcodeQuery, ProductLookupDto?>
{
    public async Task<ProductLookupDto?> Handle(GetProductByBarcodeQuery request, CancellationToken cancellationToken)
    {
        var barcode = await db.Barcodes
            .Where(b => b.Code == request.Code)
            .Select(b => new { b.ProductId, b.PackQty, ProductName = b.Product.Name, UnitName = b.Product.Unit.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (barcode is null)
            return null;

        var prices = await db.ProductPrices
            .Where(pp => pp.ProductId == barcode.ProductId && (pp.WarehouseId == request.WarehouseId || pp.WarehouseId == null))
            .ToListAsync(cancellationToken);

        var sellingPrice = (prices.FirstOrDefault(p => p.WarehouseId == request.WarehouseId)
            ?? prices.FirstOrDefault(p => p.WarehouseId == null))?.SellingPrice ?? 0;

        var onHand = await db.Stocks
            .Where(s => s.ProductId == barcode.ProductId && s.WarehouseId == request.WarehouseId)
            .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0;

        return new ProductLookupDto(barcode.ProductId, barcode.ProductName, barcode.UnitName, barcode.PackQty, sellingPrice, onHand);
    }
}

using Cartex.Persistence;
using Cartex.Application.Common.Finance;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductByBarcodeQuery(string Code, long WarehouseId, bool ForSale = false) : IRequest<ProductLookupDto?>;

public record ProductLookupDto(
    long VariantId,
    string ProductName,
    string UnitName,
    decimal PackQty,
    decimal SellingPrice,
    decimal OnHand,
    string Dimension,
    string? ImageKey = null,
    bool AllowsAmountEntry = false,
    decimal? OriginalSellingPrice = null,
    string? PriceCurrency = null,
    string? BaseCurrency = null,
    decimal ConversionRate = 1,
    bool AllowsFractional = false);

public sealed class GetProductByBarcodeQueryHandler(IApplicationDbContext db, ICurrencyService currency) : IRequestHandler<GetProductByBarcodeQuery, ProductLookupDto?>
{
    public async Task<ProductLookupDto?> Handle(GetProductByBarcodeQuery request, CancellationToken cancellationToken)
    {
        var barcode = await db.Barcodes
            .Where(b => b.Code == request.Code)
            .Select(b => new { b.VariantId, b.PackQty, ProductName = b.Variant.Product.Name, UnitName = b.Variant.Product.Unit.Name, Dimension = b.Variant.Product.Unit.Dimension, b.Variant.Product.IsEnabled, b.Variant.Product.AmountEntryEnabled, b.Variant.Product.FractionalOverride, b.Variant.Product.Unit.AllowFractional, b.Variant.Product.Unit.DefaultAllowAmountEntry, ImageKey = b.Variant.ImageKey ?? b.Variant.Product.ImageKey })
            .FirstOrDefaultAsync(cancellationToken);

        barcode ??= await db.ProductVariants
            .Where(v => v.Code == request.Code)
            .Select(v => new { VariantId = v.Id, PackQty = 1m, ProductName = v.Product.Name, UnitName = v.Product.Unit.Name, Dimension = v.Product.Unit.Dimension, v.Product.IsEnabled, v.Product.AmountEntryEnabled, v.Product.FractionalOverride, v.Product.Unit.AllowFractional, v.Product.Unit.DefaultAllowAmountEntry, ImageKey = v.ImageKey ?? v.Product.ImageKey })
            .FirstOrDefaultAsync(cancellationToken);

        if (barcode is null || (request.ForSale && !barcode.IsEnabled))
            return null;

        var prices = await db.ProductPrices
            .Where(pp => pp.VariantId == barcode.VariantId && (pp.WarehouseId == request.WarehouseId || pp.WarehouseId == null))
            .ToListAsync(cancellationToken);

        var price = prices.FirstOrDefault(p => p.WarehouseId == request.WarehouseId)
            ?? prices.FirstOrDefault(p => p.WarehouseId == null);
        var baseCurrency = await currency.BaseAsync(cancellationToken);
        var rate = price is null || price.Currency == baseCurrency
            ? 1m
            : await currency.RateAsync(price.Currency, cancellationToken);
        var sellingPrice = price is null ? 0 : Math.Round(price.SellingPrice * rate, 2);

        var onHand = await db.Stocks
            .Where(s => s.VariantId == barcode.VariantId && s.WarehouseId == request.WarehouseId)
            .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0;

        var allowsAmountEntry = barcode.AmountEntryEnabled ?? barcode.DefaultAllowAmountEntry;
        var allowsFractional = barcode.FractionalOverride ?? barcode.AllowFractional;
        return new ProductLookupDto(
            barcode.VariantId,
            barcode.ProductName,
            barcode.UnitName,
            barcode.PackQty,
            sellingPrice,
            onHand,
            barcode.Dimension.ToString(),
            barcode.ImageKey,
            allowsAmountEntry,
            price?.SellingPrice,
            price?.Currency ?? baseCurrency,
            baseCurrency,
            rate,
            allowsFractional);
    }
}

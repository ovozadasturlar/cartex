using Cartex.Application.Common.Messaging;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

// LastPurchasePrice saqlash birligida; LastEntry* — oxirgi kirim aynan qanday kiritilgani.
public record VariantPriceInfoDto(
    decimal? LastPurchasePrice,
    decimal? SellingPrice,
    long? LastUnitId = null,
    decimal? LastPackSize = null,
    long? LastPackId = null,
    decimal? LastEntryPrice = null,
    string? LastPriceBasis = null);

public record GetVariantPriceInfoQuery(long VariantId, long WarehouseId) : IRequest<VariantPriceInfoDto>;

public sealed class GetVariantPriceInfoQueryHandler(IApplicationDbContext db) : IRequestHandler<GetVariantPriceInfoQuery, VariantPriceInfoDto>
{
    public async Task<VariantPriceInfoDto> Handle(GetVariantPriceInfoQuery request, CancellationToken cancellationToken)
    {
        var lastPurchase = await db.Stocks
            .Where(s => s.VariantId == request.VariantId)
            .OrderByDescending(s => s.Id)
            .Select(s => (decimal?)s.PurchasePrice)
            .FirstOrDefaultAsync(cancellationToken);

        var selling = await db.ProductPrices
            .Where(p => p.VariantId == request.VariantId && (p.WarehouseId == request.WarehouseId || p.WarehouseId == null))
            .OrderBy(p => p.WarehouseId == null)
            .Select(p => (decimal?)p.SellingPrice)
            .FirstOrDefaultAsync(cancellationToken);

        var lastLine = await db.SupplyItems
            .Where(i => i.VariantId == request.VariantId && !i.Supply.IsDeleted)
            .OrderByDescending(i => i.Id)
            .Select(i => new { i.UnitId, i.PackSize, i.PackId, i.EntryPrice, i.PriceBasis })
            .FirstOrDefaultAsync(cancellationToken);

        return new VariantPriceInfoDto(lastPurchase, selling, lastLine?.UnitId, lastLine?.PackSize,
            lastLine?.PackId, lastLine?.EntryPrice, lastLine?.PriceBasis.ToString());
    }
}

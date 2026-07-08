using Cartex.Application.Common.Messaging;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record VariantPriceInfoDto(decimal? LastPurchasePrice, decimal? SellingPrice);

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

        return new VariantPriceInfoDto(lastPurchase, selling);
    }
}

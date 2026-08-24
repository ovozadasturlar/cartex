using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Cartex.Shared.Search;
using Cartex.Shared.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.ProductReference.Queries;

public sealed record GetProductReferenceByBarcodeQuery(string Barcode) : IRequest<ProductReferenceDto?>;

public sealed class GetProductReferenceByBarcodeQueryHandler(IApplicationDbContext db, ISettingsService settings)
    : IRequestHandler<GetProductReferenceByBarcodeQuery, ProductReferenceDto?>
{
    public async Task<ProductReferenceDto?> Handle(GetProductReferenceByBarcodeQuery request, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        if (!config.IsEnabled) return null;
        var barcode = ProductReference.Commands.ProductReferenceText.Clean(request.Barcode, 60);
        if (barcode.Length == 0) return null;
        return await db.ProductReferences.AsNoTracking()
            .Where(x => x.Barcode == barcode)
            .Select(x => new ProductReferenceDto(
                x.Barcode, x.Name, x.UnitHint, x.CategoryHint, x.ManufacturerHint, x.PackQty,
                config.AutoFillPrice ? x.SuggestedPrice : null, x.SourceKey, x.SyncedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed record SearchProductReferenceQuery(string? Search, int Take = 20) : IRequest<IReadOnlyList<ProductReferenceDto>>;

public sealed class SearchProductReferenceQueryHandler(IApplicationDbContext db, ISettingsService settings)
    : IRequestHandler<SearchProductReferenceQuery, IReadOnlyList<ProductReferenceDto>>
{
    public async Task<IReadOnlyList<ProductReferenceDto>> Handle(SearchProductReferenceQuery request, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        if (!config.IsEnabled) return [];
        var search = ProductReference.Commands.ProductReferenceText.Clean(request.Search, 200);
        var take = Math.Clamp(request.Take, 1, 100);
        var query = db.ProductReferences.AsNoTracking();
        if (search.Length > 0)
        {
            var pattern = $"%{search}%";
            var folded = SearchFold.Fuzzy(search);
            var foldedPattern = $"%{folded}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern)
                || (x.SearchFold != null && EF.Functions.ILike(x.SearchFold, foldedPattern)));
        }
        return await query.OrderBy(x => x.Name).Take(take)
            .Select(x => new ProductReferenceDto(
                x.Barcode, x.Name, x.UnitHint, x.CategoryHint, x.ManufacturerHint, x.PackQty,
                config.AutoFillPrice ? x.SuggestedPrice : null, x.SourceKey, x.SyncedAt))
            .ToListAsync(cancellationToken);
    }
}

using Cartex.Application.Common.Settings;

namespace Cartex.Application.Common.Interfaces;

public interface IProductReferenceSource
{
    Task<IReadOnlyList<ProductReferenceRow>> FetchAsync(ProductReferenceSourceConfig config, CancellationToken cancellationToken);
}

public sealed record ProductReferenceRow(
    string? Barcode,
    string? Name,
    string? UnitHint,
    string? CategoryHint,
    string? ManufacturerHint,
    decimal? PackQty,
    decimal? SuggestedPrice,
    string SourceKey);

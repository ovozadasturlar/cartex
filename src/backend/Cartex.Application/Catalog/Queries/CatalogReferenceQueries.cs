using Cartex.Shared.Models.Catalog;

namespace Cartex.Application.Catalog.Queries;

public sealed record GetCatalogReferenceByBarcodeQuery(string Code) : IRequest<CatalogProductDto?>;

public sealed class GetCatalogReferenceByBarcodeQueryHandler(ICatalogReference reference)
    : IRequestHandler<GetCatalogReferenceByBarcodeQuery, CatalogProductDto?>
{
    public Task<CatalogProductDto?> Handle(GetCatalogReferenceByBarcodeQuery request, CancellationToken cancellationToken) =>
        reference.ByBarcodeAsync(request.Code, cancellationToken);
}

public sealed record SearchCatalogReferenceQuery(string? Query, int Limit = 10) : IRequest<IReadOnlyList<CatalogProductDto>>;

public sealed class SearchCatalogReferenceQueryHandler(ICatalogReference reference)
    : IRequestHandler<SearchCatalogReferenceQuery, IReadOnlyList<CatalogProductDto>>
{
    public Task<IReadOnlyList<CatalogProductDto>> Handle(SearchCatalogReferenceQuery request, CancellationToken cancellationToken) =>
        reference.SearchAsync(request.Query, request.Limit, cancellationToken);
}

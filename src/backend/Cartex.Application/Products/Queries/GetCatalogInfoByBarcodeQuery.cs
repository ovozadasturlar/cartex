using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Products.Queries;

public record GetCatalogInfoByBarcodeQuery(string Barcode) : IRequest<ProductCatalogInfo?>;

public sealed class GetCatalogInfoByBarcodeQueryHandler(IProductCatalogProvider provider)
    : IRequestHandler<GetCatalogInfoByBarcodeQuery, ProductCatalogInfo?>
{
    public Task<ProductCatalogInfo?> Handle(GetCatalogInfoByBarcodeQuery request, CancellationToken cancellationToken) =>
        provider.LookupByBarcodeAsync(request.Barcode, cancellationToken);
}

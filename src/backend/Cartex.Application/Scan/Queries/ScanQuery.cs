using Cartex.Application.Catalog;
using Cartex.Application.Customers.Queries;
using Cartex.Application.Ordering.Queries;
using Cartex.Application.Prepacks.Queries;
using Cartex.Application.Products.Queries;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Shared.Barcodes;
using Cartex.Shared.Models.Scan;

namespace Cartex.Application.Scan.Queries;

public sealed record ScanQuery(string Code, long WarehouseId, bool ForSale = false) : IRequest<ScanResultDto>;

public sealed class ScanQueryHandler(
    ISender sender,
    ICatalogReference reference,
    ICurrentUser currentUser,
    IFeatureStateProvider features)
    : IRequestHandler<ScanQuery, ScanResultDto>
{
    private const int CartCodeLength = 32;
    private const string PrepackPrefix = "PP";

    public async Task<ScanResultDto> Handle(ScanQuery request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        if (code.Length == 0)
            return new ScanResultDto(ScanKind.None);

        if (code.Length == CartCodeLength && code.All(char.IsAsciiHexDigit)
            && await sender.Send(new GetCartByCodeQuery(code), cancellationToken) is { } cart)
            return new ScanResultDto(ScanKind.Cart, Cart: cart);

        if (await sender.Send(new GetProductByBarcodeQuery(code, request.WarehouseId, request.ForSale), cancellationToken) is { } product)
            return new ScanResultDto(ScanKind.Product, Product: product);

        if (WeightedBarcode.TryParse(code, out var weightedCode, out var weight)
            && await sender.Send(new GetProductByBarcodeQuery(weightedCode, request.WarehouseId, request.ForSale), cancellationToken) is { } weighed)
            return new ScanResultDto(ScanKind.Product, Product: weighed, Quantity: weight);

        if (code.StartsWith(PrepackPrefix, StringComparison.Ordinal)
            && currentUser.HasPermission(AppPermissions.Sales.Create)
            && await features.IsEnabledAsync(FeatureCatalog.Prepack, cancellationToken)
            && await sender.Send(new GetPrepackByCodeQuery(code, request.WarehouseId), cancellationToken) is { } prepack)
            return new ScanResultDto(ScanKind.Prepack, Prepack: prepack);

        if (currentUser.HasPermission(AppPermissions.Customers.View)
            && await sender.Send(new GetCustomerByCardQuery(code), cancellationToken) is { } customer)
            return new ScanResultDto(ScanKind.Customer, Customer: customer);

        if (await reference.ByBarcodeAsync(code, cancellationToken) is { } catalog)
            return new ScanResultDto(ScanKind.Reference, Reference: catalog);

        return new ScanResultDto(ScanKind.None);
    }
}

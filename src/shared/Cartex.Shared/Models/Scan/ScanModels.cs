using Cartex.Shared.Models.Catalog;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Ordering;
using Cartex.Shared.Models.Prepacks;
using Cartex.Shared.Models.Products;

namespace Cartex.Shared.Models.Scan;

public static class ScanKind
{
    public const string Product = "product";
    public const string Prepack = "prepack";
    public const string Customer = "customer";
    public const string Cart = "cart";
    public const string Reference = "reference";
    public const string None = "none";
}

public sealed record ScanResultDto(
    string Kind,
    ProductLookupDto? Product = null,
    PrepackLookupDto? Prepack = null,
    CustomerDto? Customer = null,
    CartDto? Cart = null,
    CatalogProductDto? Reference = null,
    decimal? Quantity = null);
